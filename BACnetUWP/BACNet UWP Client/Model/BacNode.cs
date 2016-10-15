using BACNetLib.IO.BACnet;

namespace BACNet_UWP_Client.Model
{
    public class BacNode
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

        public BacNode(BacnetAddress adr, uint device_id)
        {
            this.adr = adr;
            this.device_id = device_id;
            this.DisplayString = "DeviceId: " + device_id;
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