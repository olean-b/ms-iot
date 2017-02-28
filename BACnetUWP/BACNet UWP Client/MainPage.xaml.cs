using BACNetLib.IO.BACnet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BACNet_UWP_Client.Model;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BACNet_UWP_Client
{

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly uint Subscriptions_ReplacementPollingPeriod = 120;
        private DispatcherTimer SubscriptionRenewDispatcher;

        private uint m_next_subscription_id = 0;

        static BacnetClient bacnet_client;
        // All the present Bacnet Device List

        private ObservableCollection<BacNode> NetworkDevices = new ObservableCollection<BacNode>();

        private Dictionary<string, Subscription> m_subscription_list = new Dictionary<string, Subscription>();
        public List<KeyValuePair<BacnetAddress, uint>> Devices = new List<KeyValuePair<BacnetAddress, uint>>();

        // List containing all available local HostName endpoints
        private List<LocalHostItem> localHostItems = new List<LocalHostItem>();
        

        public MainPage()
        {
            this.InitializeComponent();



            COVSubscriptionsListView.ItemsSource = m_subscription_list.Values;
            DevicesListView.ItemsSource = NetworkDevices;

            this.PopulateAdapterList();
            AdapterList.IsEnabled = true;


            // Create timer for subscription renewal
            // 
            SubscriptionRenewDispatcher = new DispatcherTimer();
            SubscriptionRenewDispatcher.Tick += SubscriptionRenewDispatcher_Tick;
            SubscriptionRenewDispatcher.Interval = new TimeSpan(0, 0, 0, 15);
            SubscriptionRenewDispatcher.Start();
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

                        if (!sub.comm.SubscribeCOVRequest(sub.adr, sub.object_id, sub.subscribe_id, false, false, Subscriptions_ReplacementPollingPeriod))
                        {
                            // SetSubscriptionStatus(l_item, "Offline");
                            Debug.WriteLine("Couldn't renew subscription " + sub.subscribe_id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Exception during renew subscription: " + ex.Message);
                    }
                    this.UiLogItem($"Renew subscription on {l_item.Key}");
                }
        }


        private void InitCOVClient_Click(object sender, RoutedEventArgs e)
        {

            UiLogItem("Init Client");

            var selectedLocalHost = (LocalHostItem)AdapterList.SelectedItem;
            if (selectedLocalHost == null)
            {
                UiLogItem("Please select an address / adapter.");
                return;
            }

            bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false, false, 1472, selectedLocalHost.LocalHost.CanonicalName), 1000, 3);
            bacnet_client.Start();
            // Send WhoIs in order to get back all the Iam responses :  
            bacnet_client.OnIam += handler_OnIam;
            bacnet_client.OnCOVNotification += handler_OnCOVNotification;

            bacnet_client.WhoIs();
        }

        private void UiLogItem(string message)
        {
            object l_logItem = $"{DateTime.Now.ToString("t")} {message}";

            BacLog.Items.Add(l_logItem);

            var selectedIndex = BacLog.Items.Count - 1;
            if (selectedIndex < 0)
            {
                return;                
            }

            BacLog.SelectedIndex = selectedIndex;
            BacLog.UpdateLayout();

            BacLog.ScrollIntoView(BacLog.SelectedItem);

           // BacLog.ScrollIntoView(l_logItem);
        }

        private void InitSubscription_Click(object sender, RoutedEventArgs e)
        {
           uint l_device_id = 127001;
           // uint l_device_id = 20205;

            UiLogItem("Start subscription");

            BacnetAddress adr;

            adr = DeviceAddr(l_device_id);
            if (adr == null)
            {
                UiLogItem("Failed. Client not found");
                return;
            }

            // advise to OBJECT_ANALOG_INPUT:1 provided by the device 1026
                     var l_objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 10240);
            //var l_objectId = new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 3);

            bacnet_client.SubscribeCOVRequest(adr, l_objectId, 0, false, false, 160);

            m_next_subscription_id++;
            string sub_key = adr.ToString() + ":" + l_device_id + ":" + m_next_subscription_id;

            var subScriptionObj = new Subscription(bacnet_client, adr, new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE, l_device_id), l_objectId, sub_key, m_next_subscription_id);

            m_subscription_list.Add(sub_key, subScriptionObj);
        }



        void ReadWriteExample()
        {

            BacnetValue Value;
            bool ret;
            // Read Present_Value property on the object ANALOG_INPUT:0 provided by the device 12345
            // Scalar value only
            // ret = ReadScalarValue(20205, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 3), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value); // Sauter
            ret = ReadScalarValue(127001, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 6923), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value); // LM

            if (ret == true)
            {
                UiLogItem($"Read value : {Value.Value}");
                // Write Present_Value property on the object ANALOG_OUTPUT:0 provided by the device 4000
                BacnetValue newValue = new BacnetValue(Convert.ToSingle(Value.Value));   // expect it's a float
                                                                                         //ret = WriteScalarValue(20205, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, newValue);

                Debug.WriteLine("Write feedback : " + ret.ToString());
            }
            else
                Debug.WriteLine("Error somewhere !");
        }

        void handler_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
        {
            // Device already registred ?
            foreach (BacNode bn in NetworkDevices)
            {
                if (bn.getAdd(device_id) != null)
                {
                    return;
                }
            }

            var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => NetworkDevices.Add(new BacNode(adr, device_id)));
        }

        /*****************************************************************************************************/
        void handler_OnCOVNotification(BacnetClient sender, BacnetAddress adr, byte invoke_id, uint subscriberProcessIdentifier, BacnetObjectId initiatingDeviceIdentifier, BacnetObjectId monitoredObjectIdentifier, uint timeRemaining, bool need_confirm, ICollection<BacnetPropertyValue> values, BacnetMaxSegments max_segments)
        {
            foreach (BacnetPropertyValue value in values)
            {
                switch ((BacnetPropertyIds)value.property.propertyIdentifier)
                {
                    case BacnetPropertyIds.PROP_PRESENT_VALUE:
                        var ignore2 = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            UiLogItem($"Got {value.value[0].Value} from {monitoredObjectIdentifier}");
                        });

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


        /*****************************************************************************************************/
        bool ReadScalarValue(int device_id, BacnetObjectId BacnetObjet, BacnetPropertyIds Propriete, out BacnetValue Value)
        {
            BacnetAddress adr;
            IList<BacnetValue> NoScalarValue;

            Value = new BacnetValue(null);

            // Looking for the device
            adr = DeviceAddr((uint)device_id);
            if (adr == null) return false;  // not found

            // Property Read
            if (bacnet_client.ReadPropertyRequest(adr, BacnetObjet, Propriete, out NoScalarValue) == false)
                return false;

            Value = NoScalarValue[0];
            return true;
        }

        BacnetAddress DeviceAddr(uint device_id)
        {
            BacnetAddress ret;

            foreach (BacNode bn in NetworkDevices)
            {
                ret = bn.getAdd(device_id);
                if (ret != null) return ret;
            }
            // not in the list
            return null;

        }

        private void EndCOVClient_Click(object sender, RoutedEventArgs e)
        {
            var deletes = new List<string>();

            foreach (var subscription in m_subscription_list)
            {

                if (subscription.Value.is_active_subscription)
                {
                    if (
                        !subscription.Value.comm.SubscribeCOVRequest(subscription.Value.adr,subscription.Value.object_id, subscription.Value.subscribe_id, true, false, 0))
                    {
                        UiLogItem($"Couldn't unsubscribe objectr {subscription.Value.object_id}");
                    }

                    deletes.Add(subscription.Key);

                }
            }

            foreach (var item in deletes)
            {
                lock (m_subscription_list)
                {
                    this.m_subscription_list.Remove(item);
                    UiLogItem($"Subscription removed - {item}");
                }
            }


        }

        /// <summary>
        /// Populates the NetworkAdapter list
        /// </summary>
        private void PopulateAdapterList()
        {
            localHostItems.Clear();
            AdapterList.ItemsSource = localHostItems;
            AdapterList.DisplayMemberPath = "DisplayString";

            foreach (var localHostInfo in NetworkInformation.GetHostNames())
            {
                if (localHostInfo.IPInformation != null)
                {
                    LocalHostItem adapterItem = new LocalHostItem(localHostInfo);
                    localHostItems.Add(adapterItem);

                    //AdapterList.SelectedIndex = 0;
                }
            }            
        }

        private void OutputLog_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        /// <summary>
        /// Used to display messages to the user
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// Helper class describing a NetworkAdapter and its associated IP address
    /// </summary>
    class LocalHostItem
    {
        public string DisplayString
        {
            get;
            private set;
        }

        public HostName LocalHost
        {
            get;
            private set;
        }

        public LocalHostItem(HostName localHostName)
        {
            if (localHostName == null)
            {
                throw new ArgumentNullException("localHostName");
            }

            if (localHostName.IPInformation == null)
            {
                throw new ArgumentException("Adapter information not found");
            }

            this.LocalHost = localHostName;
            this.DisplayString = "Address: " + localHostName.DisplayName;
        }
    }

    class Subscription
    {
        public BacnetClient comm { get; set; }
        public BacnetAddress adr { get; set; }
        public BacnetObjectId device_id { get; set; }
        public BacnetObjectId object_id { get; set; }
        public string sub_key { get; set; }
        public uint subscribe_id;
        public bool is_active_subscription = true; // false if subscription is refused

        public Subscription(BacnetClient comm, BacnetAddress adr, BacnetObjectId device_id, BacnetObjectId object_id, string sub_key, uint subscribe_id)
        {
            this.comm = comm;
            this.adr = adr;
            this.device_id = device_id;
            this.object_id = object_id;
            this.sub_key = sub_key;
            this.subscribe_id = subscribe_id;
        }
    }

    public sealed class SubscriptionRenewalTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {


        }
    }
}
