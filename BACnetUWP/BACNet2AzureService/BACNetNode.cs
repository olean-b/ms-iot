using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BACNetLib.IO.BACnet;

namespace BACNet2AzureService
{
    class BACNetNode
    {
        public string DisplayString
        {
            get;
            private set;
        }

        public string DeviceName
        {
            get
            {
                return device_id.ToString();
            }
        }

        BacnetAddress adr;
        uint device_id;

        public BACNetNode(BacnetAddress adr, uint device_id)
        {
            this.adr = adr;
            this.device_id = device_id;
            this.DisplayString = "DeviceId: " + device_id;

            Debug.WriteLine($"Added deviceId {device_id} on address {adr}");
        }

        public BacnetAddress GetDeviceAddress(uint device_id)
        {
            if (this.device_id == device_id)
            {
                return adr;                
            }

            return null;
        }
    }
}
