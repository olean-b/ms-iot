using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Windows.ApplicationModel.Background;
using Microsoft.Azure.Devices.Client;
using KNXLibPortableLib;

using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace HeadlessKnx2AzureGateway
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static KnxTunneling m_knxConnection;
        private DeviceClient m_deviceClient;

        private static readonly IList<string> Temperatures = new List<string> { "3/3/10", "3/3/11", "3/3/12", "3/3/13", "3/3/14", "1/0/38", "1/0/39", "1/0/22", "1/0/2", "1/0/40", "1/1/6", "3/0/1"}; // Gernerl.             
      //  private static readonly IList<string> Temperatures = new List<string> { "3/3/2"}; // Gernerl. 

            // Needed to make sure the application keeps running in the background
        private BackgroundTaskDeferral _backgroundTaskDeferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

             m_deviceClient = DeviceClient.CreateFromConnectionString("HostName=Gernerlunden10.azure-devices.net;DeviceId=RO-WinBerry01;SharedAccessKey=tDdNQ5AIYfwcaPuNodXw/7EwouYfbd3js6oT5XtayUU=", Microsoft.Azure.Devices.Client.TransportType.Http1);
            // m_deviceClient = DeviceClient.CreateFromConnectionString("HostName=Gernerlunden10.azure-devices.net;DeviceId=RO-WinBerry01;SharedAccessKeyName=iothubowner;SharedAccessKey=UkQ3yXAoIt4xPO+agswjJd3zm7lOlEmwQ+C1f9tnJHg=", Microsoft.Azure.Devices.Client.TransportType.Http1);

            
            m_knxConnection = new KnxTunneling("192.168.1.140", 3671, "192.168.1.117", 3671) { IsDebug = false }; // Gernerl.             

            m_knxConnection.OnConnected += Connected;
            m_knxConnection.OnEvent += Event;
           // m_knxConnection.OnStatus += Status;

            m_knxConnection.OnDisconnected += Disconnected;

            m_knxConnection.Connect();

            Debug.WriteLine("Init. Done");
        }


        private static void Connected()
        {
            Debug.WriteLine("Connected!");
        }

        private static async void Disconnected()
        {
            Debug.WriteLine("Disconnected! Reconnecting");
            if (m_knxConnection == null)
                return;

            await Task.Delay(TimeSpan.FromSeconds(5));

            m_knxConnection.Connect();
        }

        private async void Event(string address, string data)
        {
            //if (Temperatures.Contains(address))
            //{
            //    var temp = m_knxConnection.FromDataPoint("9.001", data);
            //    Debug.WriteLine($"New Event: TEMPERATURE on GA {address} \t value {temp}");
            //}
            //else
            //{
            //    Debug.WriteLine("New Event: device " + address + " has data " + data);
            //}

            if (Temperatures.Contains(address))
            {
                var l_knxDataPoint = m_knxConnection.FromDataPoint("9.001", data);

                var l_telemetry = new TelemetryMessage() {
                    DeviceId = "RO-WinBerry01",
                    KNXMessageSource = address,
                    KNXTextualValue = l_knxDataPoint.ToString(),
                    KNXDataType = "9.001",
                    LocalTimeStamp = DateTime.Now
                };

                var l_outBoundMessage = new Message(Serialize(l_telemetry));

                if (m_deviceClient != null)
                {
                   await m_deviceClient.SendEventAsync(l_outBoundMessage);
                }

                Debug.WriteLine($"Sent telemetry data to IoT Suite \t DataPoint GA={address} \t Data={l_knxDataPoint.ToString()}");
            }
            else
            {
                Debug.WriteLine($"Got message from {address}");
            }
        }

        private static void Status(string address, string state)
        {
            Debug.WriteLine("New Status: device " + address + " has status " + state);
        }

        private byte[] Serialize(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            return Encoding.UTF8.GetBytes(json);
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            //a few reasons that you may be interested in.
            switch (reason)
            {
                case BackgroundTaskCancellationReason.Abort:
                    //app unregistered background task (amoung other reasons).
                    break;
                case BackgroundTaskCancellationReason.Terminating:
                    //system shutdown
                    break;
                case BackgroundTaskCancellationReason.ConditionLoss:
                    break;
                case BackgroundTaskCancellationReason.SystemPolicy:
                    break;
            }

            _backgroundTaskDeferral.Complete();
        }
    }
}
