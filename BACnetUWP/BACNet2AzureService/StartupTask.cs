using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Microsoft.Azure.Devices.Client;
using BACNetLib.IO.BACnet;
using Newtonsoft.Json;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace BACNet2AzureService
{

    public sealed class StartupTask : IBackgroundTask
    {
        //
        // Note: this connection string is specific to the device "RO-WinBerry01". To configure other devices,
        // see information on iothub-explorer at http://aka.ms/iothubgetstartedVSCS
        //
        const string deviceConnectionString = "HostName=Gernerlunden10.azure-devices.net;DeviceId=RO-WinBerry01;SharedAccessKey=tDdNQ5AIYfwcaPuNodXw/7EwouYfbd3js6oT5XtayUU=";

        // Needed to make sure the application keeps running in the background
        private BackgroundTaskDeferral m_backgroundTaskDeferral;
        private DeviceClient m_azureDeviceClient;

        readonly uint Subscriptions_ReplacementPollingPeriod = 500;

        private ThreadPoolTimer subscriptionRenewalTimer;
        private Dictionary<string, Subscription> m_subscription_list = new Dictionary<string, Subscription>();

        private List<BACNetNode> NetworkDevices = new List<BACNetNode>();

        private uint m_next_subscription_id = 0;

        static BacnetClient m_bacnetClient;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.m_backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            this.m_azureDeviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Amqp);

            // Set-up COV Subscription - Create a timer that run each second and changes the value
            subscriptionRenewalTimer = ThreadPoolTimer.CreatePeriodicTimer(subscriptionRenewalTimer_Tick, TimeSpan.FromSeconds(Subscriptions_ReplacementPollingPeriod / 2));

            m_bacnetClient = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false, false, 1472, "192.168.1.117"), 1000, 3);
            m_bacnetClient.Start();
            // Send WhoIs in order to get back all the Iam responses :  
            m_bacnetClient.OnIam += handler_OnIam;
            m_bacnetClient.OnCOVNotification += handler_OnCOVNotification;

            m_bacnetClient.WhoIs();

            ThreadPoolTimer.CreateTimer(DelayLoadSubscriptions, TimeSpan.FromSeconds(5));
        }

        private void DelayLoadSubscriptions(ThreadPoolTimer timer)
        {
            // LogicMachine BACNet adapter
            uint l_device = 127001;
            

            // Innluft temp.
            this.CreateSubscription(6926, BacnetObjectTypes.OBJECT_ANALOG_VALUE, l_device);

            // Avtrekk temp.
            this.CreateSubscription(6923, BacnetObjectTypes.OBJECT_ANALOG_VALUE, l_device); 

            // Utelys (binary)
            this.CreateSubscription(8201, BacnetObjectTypes.OBJECT_BINARY_VALUE, l_device);

            // Count
            this.CreateSubscription(10240, BacnetObjectTypes.OBJECT_ANALOG_VALUE, l_device);
            
        }

        private void CreateSubscription(uint l_objectInstance, BacnetObjectTypes objectType, uint l_device)
        {            
            var l_objectId = new BacnetObjectId(objectType, l_objectInstance);

            var l_address = this.GetBacnetAddress(l_device);

            if (l_address == null)
            {
                Debug.WriteLine($"Failed. Client {l_device} not found");
                return;
            }

            this.m_next_subscription_id++;

            m_bacnetClient.SubscribeCOVRequest(l_address, l_objectId, this.m_next_subscription_id, false, false, 160);            

            var l_subscriptionKey = l_address + ":" + l_device + ":" + this.m_next_subscription_id;

            var l_subscription = new Subscription(
                                     m_bacnetClient,
                                     l_address,
                                     new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE, l_device),
                                     l_objectId,
                                     l_subscriptionKey,
                                     this.m_next_subscription_id);

            this.m_subscription_list.Add(l_subscriptionKey, l_subscription);
        }

        private void subscriptionRenewalTimer_Tick(ThreadPoolTimer timer)
        {
            foreach (var l_item in m_subscription_list)
            {
                try
                {
                    Subscription sub = l_item.Value;

                    if (sub.is_active_subscription == false) // not needs to renew, periodic pooling in operation (or nothing) due to COV subscription refused by the remote device
                        return;

                    if (!sub.m_commClient.SubscribeCOVRequest(sub.m_address, sub.object_id, sub.subscribe_id, false, false, Subscriptions_ReplacementPollingPeriod))
                    {
                        // SetSubscriptionStatus(l_item, "Offline");
                        Debug.WriteLine("Couldn't renew subscription " + sub.subscribe_id);
                    }
                }
                catch (Exception ex)
                {
                    //Trace.TraceError("Exception during renew subscription: " + ex.Message);
                }
                Debug.WriteLine($"Renew subscription on {l_item.Key}");
            }
        }

        void handler_OnIam(BacnetClient sender, BacnetAddress adr, uint deviceId, uint maxApdu, BacnetSegmentations segmentation, ushort vendorId)
        {
            // Device already registred ?
            foreach (var l_device in NetworkDevices)
            {
                if (l_device.GetDeviceAddress(deviceId) != null)
                {
                    return;
                }
            }

            NetworkDevices.Add(new BACNetNode(adr, deviceId));
        }

        void handler_OnCOVNotification(BacnetClient sender, BacnetAddress adr, byte invoke_id, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool need_confirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments max_segments)
        {
            foreach (BacnetPropertyValue value in values)
            {
                switch ((BacnetPropertyIds)value.property.propertyIdentifier)
                {
                    case BacnetPropertyIds.PROP_PRESENT_VALUE:
                        Debug.WriteLine($"Got {value.value[0].Value} from {monitoredObjectIdentifier}");
                        this.SendTelemtry(value, monitoredObjectIdentifier);
                        break;
                    case BacnetPropertyIds.PROP_STATUS_FLAGS:
                        string status_text = "";
                        if (value.value != null && value.value.Count > 0)
                        {
                            BacnetStatusFlags status = (BacnetStatusFlags)((BacnetBitString)value.value[0].Value).ConvertToInt();
                            if ((status & BacnetStatusFlags.STATUS_FLAG_FAULT) == BacnetStatusFlags.STATUS_FLAG_FAULT)
                                status_text += "FAULT,";
                            else if ((status & BacnetStatusFlags.STATUS_FLAG_IN_ALARM) == BacnetStatusFlags.STATUS_FLAG_IN_ALARM)
                                status_text += "ALARM,";
                            else if ((status & BacnetStatusFlags.STATUS_FLAG_OUT_OF_SERVICE) == BacnetStatusFlags.STATUS_FLAG_OUT_OF_SERVICE)
                                status_text += "OOS,";
                            else if ((status & BacnetStatusFlags.STATUS_FLAG_OVERRIDDEN) == BacnetStatusFlags.STATUS_FLAG_OVERRIDDEN)
                                status_text += "OR,";
                        }
                        if (status_text != "")
                            Debug.WriteLine(status_text);
                        break;
                    default:
                        //got something else? ignore it
                        break;
                }
            }

            if (need_confirm)
            {
                sender.SimpleAckResponse(adr, BacnetConfirmedServices.SERVICE_CONFIRMED_COV_NOTIFICATION, invoke_id);
            }
        }


        private async void SendTelemtry(BacnetPropertyValue value, BacnetObjectId monitoredObjectIdentifier)
        {
            var l_telemetry = new TelemetryMessage()
            {
                DeviceId = "RO-WinBerry02",
                MessageSource = monitoredObjectIdentifier.instance.ToString(),
                TextualValue = value.value[0].Value.ToString(),
                DataType = monitoredObjectIdentifier.Type.ToString(),
                LocalTimeStamp = DateTime.Now
            };

            var l_outBoundMessage = new Message(Serialize(l_telemetry));

            if (m_azureDeviceClient != null)
            {
                await m_azureDeviceClient.SendEventAsync(l_outBoundMessage);
            }

            Debug.WriteLine($"Sent telemetry data to IoT Suite \t Object{monitoredObjectIdentifier.Instance} \t Data={value.value[0].Value}");
        }


        private void SubscriptionRenewDispatcher_Tick(object sender, object e)
        {
            lock (m_subscription_list)
                foreach (var l_item in m_subscription_list)
                {
                    try
                    {
                        Subscription sub = l_item.Value;

                        if (sub.is_active_subscription == false) // not needs to renew, periodic pooling in operation (or nothing) due to COV subscription refused by the remote device
                            return;

                        if (!sub.m_commClient.SubscribeCOVRequest(sub.m_address, sub.object_id, sub.subscribe_id, false, false, Subscriptions_ReplacementPollingPeriod))
                        {
                            // SetSubscriptionStatus(l_item, "Offline");
                            Debug.WriteLine("Couldn't renew subscription " + sub.subscribe_id);
                        }
                    }
                    catch (Exception ex)
                    {
                        //Trace.TraceError("Exception during renew subscription: " + ex.Message);
                    }
                    Debug.WriteLine($"Renew subscription on {l_item.Key}");
                }
        }

        private static byte[] Serialize(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            return Encoding.UTF8.GetBytes(json);
        }

        private BacnetAddress GetBacnetAddress(uint deviceId)
        {
            BacnetAddress result;

            foreach (var device in NetworkDevices)
            {
                result = device.GetDeviceAddress(deviceId);
                if (result != null)
                {
                    return result;
                }
            }
            // not in the list
            return null;
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            switch (reason)
            {
                case BackgroundTaskCancellationReason.Abort:
                    //app unregistered background task (amoung other reasons).
                    break;
                case BackgroundTaskCancellationReason.Terminating:
                    //system shutdown
                    break;
                case BackgroundTaskCancellationReason.LoggingOff:
                    break;
                case BackgroundTaskCancellationReason.ServicingUpdate:
                    break;
                case BackgroundTaskCancellationReason.IdleTask:
                    break;
                case BackgroundTaskCancellationReason.Uninstall:
                    break;
                case BackgroundTaskCancellationReason.ConditionLoss:
                    break;
                case BackgroundTaskCancellationReason.SystemPolicy:
                    break;
                case BackgroundTaskCancellationReason.ExecutionTimeExceeded:
                    break;
                case BackgroundTaskCancellationReason.ResourceRevocation:
                    break;
                case BackgroundTaskCancellationReason.EnergySaver:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }

            this.m_backgroundTaskDeferral.Complete();
        }
    }

    class Subscription
    {
        public BacnetClient m_commClient { get; set; }
        public BacnetAddress m_address { get; set; }
        public BacnetObjectId device_id { get; set; }
        public BacnetObjectId object_id { get; set; }
        public string sub_key { get; set; }

        public uint subscribe_id;

        public bool is_active_subscription = true; // false if subscription is refused

        public Subscription(BacnetClient comm, BacnetAddress adr, BacnetObjectId device_id, BacnetObjectId object_id, string sub_key, uint subscribe_id)
        {
            this.m_commClient = comm;
            this.m_address = adr;
            this.device_id = device_id;
            this.object_id = object_id;
            this.sub_key = sub_key;
            this.subscribe_id = subscribe_id;
        }
    }
}
