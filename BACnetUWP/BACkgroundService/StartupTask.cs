using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using System.Diagnostics;
using BACNetLib.IO.BACnet;
using System.Threading.Tasks;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace BACkgroundService
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferal;

        static BacnetClient bacnet_client;

        // All the present Bacnet Device List
        static List<BacNode> DevicesList = new List<BacNode>();

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Debug.WriteLine("StartupTask.Run()");

            _deferal = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;


            StartActivity();

            ReadWriteExample();


        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Debug.WriteLine("StartupTask.TaskInstance_Canceled() - {0}", reason.ToString());

            _deferal.Complete();
        }

        async Task PutTaskDelay()
        {
            await Task.Delay(5000);
        }

        /*****************************************************************************************************/

        private async void StartActivity()
        {
            // Bacnet on UDP/IP/Ethernet
            //bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false));
            // Replaced ConnectionString for UDP local adapter
            //            bacnet_client = new BacnetClient(new BacnetIpUdpProtocolTransport(0xBAC0, false, false, 1472, "192.168.127.1"), 1000, 3);
            bacnet_client = new BacnetClient(new BACNetLib.IO.BACnet.BacnetIpUdpProtocolTransport(0xBAC0, false, false, 1472, "192.168.1.21"), 1000, 3);
            // or Bacnet Mstp on COM4 à 38400 bps, own master id 8
            // m_bacnet_client = new BacnetClient(new BacnetMstpProtocolTransport("COM4", 38400, 8);
            // Or Bacnet Ethernet
            // bacnet_client = new BacnetClient(new BacnetEthernetProtocolTransport("Connexion au réseau local"));          
            // Or Bacnet on IPV6
            // bacnet_client = new BacnetClient(new BacnetIpV6UdpProtocolTransport(0xBAC0));

            bacnet_client.Start();    // go

            // Send WhoIs in order to get back all the Iam responses :  
            bacnet_client.OnIam += new BacnetClient.IamHandler(handler_OnIam);

            bacnet_client.WhoIs();
            await PutTaskDelay();
        }


        static void ReadWriteExample()
        {

            BacnetValue Value;
            bool ret;
            // Read Present_Value property on the object ANALOG_INPUT:0 provided by the device 12345
            // Scalar value only
            // ret = ReadScalarValue(20205, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 3), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value); // Sauter
            ret = ReadScalarValue(127001, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 6923), BacnetPropertyIds.PROP_PRESENT_VALUE, out Value); // LM

            if (ret == true)
            {
                Debug.WriteLine("Read value : " + Value.Value.ToString());

                // Write Present_Value property on the object ANALOG_OUTPUT:0 provided by the device 4000
                BacnetValue newValue = new BacnetValue(Convert.ToSingle(Value.Value));   // expect it's a float
                //ret = WriteScalarValue(20205, new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, 0), BacnetPropertyIds.PROP_PRESENT_VALUE, newValue);

                Debug.WriteLine("Write feedback : " + ret.ToString());
            }
            else
                Debug.WriteLine("Error somewhere !");
        }

        static void handler_OnIam(BacnetClient sender, BacnetAddress adr, uint device_id, uint max_apdu, BacnetSegmentations segmentation, ushort vendor_id)
        {
            lock (DevicesList)
            {
                // Device already registred ?
                foreach (BacNode bn in DevicesList)
                    if (bn.getAdd(device_id) != null) return;   // Yes

                // Not already in the list
                DevicesList.Add(new BacNode(adr, device_id));   // add it
            }
        }

        /*****************************************************************************************************/
        static bool ReadScalarValue(int device_id, BacnetObjectId BacnetObjet, BacnetPropertyIds Propriete, out BacnetValue Value)
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

        static BacnetAddress DeviceAddr(uint device_id)
        {
            BacnetAddress ret;

            lock (DevicesList)
            {
                foreach (BacNode bn in DevicesList)
                {
                    ret = bn.getAdd(device_id);
                    if (ret != null) return ret;
                }
                // not in the list
                return null;
            }
        }
    }

    class BacNode
    {
        BacnetAddress adr;
        uint device_id;

        public BacNode(BacnetAddress adr, uint device_id)
        {
            this.adr = adr;
            this.device_id = device_id;
        }

        public BacnetAddress getAdd(uint device_id)
        {
            if (this.device_id == device_id)
                return adr;
            else
                return null;
        }
    }
}
