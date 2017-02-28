// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace HeadlessKnx2AzureGateway
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    using KNXLibPortableLib;

    using Microsoft.Azure.Devices.Client;

    using Newtonsoft.Json;

    using Windows.ApplicationModel.Background;

    /// <summary>
    /// The startup task.
    /// </summary>
    public sealed class StartupTask : IBackgroundTask
    {
        private static KnxTunneling s_knxConnection;

        private DeviceClient m_deviceClient;

        private static readonly IList<string> Temperatures = new List<string> { "3/3/10", "3/3/11", "3/3/12", "3/3/13", "3/3/14", "1/0/38", "1/0/39", "1/0/22", "1/0/2", "1/0/40", "1/1/6", "3/0/1" }; // Gernerl.             

        // Needed to make sure the application keeps running in the background
        private BackgroundTaskDeferral m_backgroundTaskDeferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            m_backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            // Key's are regenerated - no need to attempt 
            m_deviceClient = DeviceClient.CreateFromConnectionString("HostName=Gernerlunden10.azure-devices.net;DeviceId=RO-WinBerry01;SharedAccessKey=tDdNQ5AIYfwcaPuNodXw/7EwouYfbd3js6oT5XtayUU=", Microsoft.Azure.Devices.Client.TransportType.Http1);

            s_knxConnection = new KnxTunneling("192.168.1.140", 3671, "192.168.1.117", 3671) { IsDebug = false }; // Gernerl.             

            s_knxConnection.OnConnected += Connected;
            s_knxConnection.OnEvent += this.Event;
            // m_knxConnection.OnStatus += Status;

            s_knxConnection.OnDisconnected += Disconnected;

            s_knxConnection.Connect();

            Debug.WriteLine("Init. Done");
        }


        private static void Connected()
        {
            Debug.WriteLine("Connected!");
        }

        private static async void Disconnected()
        {
            Debug.WriteLine("Disconnected! Reconnecting");
            if (s_knxConnection == null)
                return;

            await Task.Delay(TimeSpan.FromSeconds(5));

            s_knxConnection.Connect();
        }

        private async void Event(string address, string data)
        {

            if (Temperatures.Contains(address))
            {
                var l_knxDataPoint = s_knxConnection.FromDataPoint("9.001", data);

                var l_telemetry = new TelemetryMessage()
                {
                    DeviceId = "RO-WinBerry02",
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

            m_backgroundTaskDeferral.Complete();
        }
    }
}
