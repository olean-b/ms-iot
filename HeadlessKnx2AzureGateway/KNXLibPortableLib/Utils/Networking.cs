namespace KNXLibPortableLib.Utils
{
    using System;
    using System.Linq;
    using System.Net;

    using KNXLibPortableLib.Exceptions;

    public static class Networking
    {
        public static ServiceType GetServiceType(byte[] datagram)
        {
            switch (datagram[2])
            {
                case (0x02):
                    {
                        switch (datagram[3])
                        {
                            case (0x06):
                                return ServiceType.ConnectionResponse;
                            case (0x09):
                                return ServiceType.DisconnectRequest;
                            case (0x08):
                                return ServiceType.ConnectionStateResponse;
                        }
                    }
                    break;
                case (0x04):
                    {
                        switch (datagram[3])
                        {
                            case (0x20):
                                return ServiceType.TunnelingRequest;
                            case (0x21):
                                return ServiceType.TunnelingAck;
                        }
                    }
                    break;
            }
            return ServiceType.Unknown;
        }

        public static IPEndPoint CreateRemoteEndpoint(string address, int port)
        {
            IPAddress remoteAddress;
            if (!IPAddress.TryParse(address, out remoteAddress))
            {
                //try
                //{
                //    DnsEndPoint endpoint = new DnsEndPoint(address, port);
                //    endpoint.
                //}
                throw new Exception("Remote endpoint could not be resolved");
            }

            return new IPEndPoint(remoteAddress, port);
        }

        //           +-----------------------------------------------+
        // 16 bits   |              INDIVIDUAL ADDRESS               |
        //           +-----------------------+-----------------------+
        //           | OCTET 0 (high byte)   |  OCTET 1 (low byte)   |
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //    bits   | 7| 6| 5| 4| 3| 2| 1| 0| 7| 6| 5| 4| 3| 2| 1| 0|
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //           |  Subnetwork Address   |                       |
        //           +-----------+-----------+     Device Address    |
        //           |(Area Adrs)|(Line Adrs)|                       |
        //           +-----------------------+-----------------------+

        //           +-----------------------------------------------+
        // 16 bits   |             GROUP ADDRESS (3 level)           |
        //           +-----------------------+-----------------------+
        //           | OCTET 0 (high byte)   |  OCTET 1 (low byte)   |
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //    bits   | 7| 6| 5| 4| 3| 2| 1| 0| 7| 6| 5| 4| 3| 2| 1| 0|
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //           |  | Main Grp  | Midd G |       Sub Group       |
        //           +--+--------------------+-----------------------+

        //           +-----------------------------------------------+
        // 16 bits   |             GROUP ADDRESS (2 level)           |
        //           +-----------------------+-----------------------+
        //           | OCTET 0 (high byte)   |  OCTET 1 (low byte)   |
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //    bits   | 7| 6| 5| 4| 3| 2| 1| 0| 7| 6| 5| 4| 3| 2| 1| 0|
        //           +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
        //           |  | Main Grp  |            Sub Group           |
        //           +--+--------------------+-----------------------+
        public static bool IsAddressIndividual(string address)
        {
            return address.Contains('.');
        }

        public static string GetIndividualAddress(byte[] addr)
        {
            return GetAddress(addr, '.', false);
        }

        public static string GetGroupAddress(byte[] addr, bool threeLevelAddressing)
        {
            return GetAddress(addr, '/', threeLevelAddressing);
        }

        private static string GetAddress(byte[] addr, char separator, bool threeLevelAddressing)
        {
            var group = separator.Equals('/');
            string address;

            if (group && !threeLevelAddressing)
            {
                // 2 level group
                address = (addr[0] >> 3).ToString();
                address += separator;
                address += (((addr[0] & 0x07) << 8) + addr[1]).ToString(); // this may not work, must be checked
            }
            else
            {
                // 3 level individual or group
                address = group
                    ? ((addr[0] & 0x7F) >> 3).ToString()
                    : (addr[0] >> 4).ToString();

                address += separator;

                if (group)
                    address += (addr[0] & 0x07).ToString();
                else
                    address += (addr[0] & 0x0F).ToString();

                address += separator;
                address += addr[1].ToString();
            }

            return address;
        }

        public static byte[] GetAddress(string address)
        {
            try
            {
                var addr = new byte[2];
                var threeLevelAddressing = true;
                string[] parts;
                var group = address.Contains('/');

                if (!group)
                {
                    // individual address
                    parts = address.Split('.');
                    if (parts.Length != 3 || parts[0].Length > 2 || parts[1].Length > 2 || parts[2].Length > 3)
                        throw new InvalidKnxAddressException(address);
                }
                else
                {
                    // group address
                    parts = address.Split('/');
                    if (parts.Length != 3 || parts[0].Length > 2 || parts[1].Length > 1 || parts[2].Length > 3)
                    {
                        if (parts.Length != 2 || parts[0].Length > 2 || parts[1].Length > 4)
                            throw new InvalidKnxAddressException(address);

                        threeLevelAddressing = false;
                    }
                }

                if (!threeLevelAddressing)
                {
                    var part = int.Parse(parts[0]);
                    if (part > 15)
                        throw new InvalidKnxAddressException(address);

                    addr[0] = (byte)(part << 3);
                    part = int.Parse(parts[1]);
                    if (part > 2047)
                        throw new InvalidKnxAddressException(address);

                    var part2 = BitConverter.GetBytes(part);
                    if (part2.Length > 2)
                        throw new InvalidKnxAddressException(address);

                    addr[0] = (byte)(addr[0] | part2[0]);
                    addr[1] = part2[1];
                }
                else
                {
                    var part = int.Parse(parts[0]);
                    if (part > 15)
                        throw new InvalidKnxAddressException(address);

                    addr[0] = group
                        ? (byte)(part << 3)
                        : (byte)(part << 4);

                    part = int.Parse(parts[1]);
                    if ((group && part > 7) || (!group && part > 15))
                        throw new InvalidKnxAddressException(address);

                    addr[0] = (byte)(addr[0] | part);
                    part = int.Parse(parts[2]);
                    if (part > 255)
                        throw new InvalidKnxAddressException(address);

                    addr[1] = (byte)part;
                }

                return addr;
            }
            catch (Exception)
            {
                throw new InvalidKnxAddressException(address);
            }
        }

        public static KnxDestinationAddressType GetKnxDestinationAddressType(byte control_field_2)
        {
            return (0x80 & control_field_2) != 0
                ? KnxDestinationAddressType.Group
                : KnxDestinationAddressType.Individual;
        }

        public static int GetChannelId(byte[] datagram)
        {
            if (datagram.Length > 6)
                return datagram[6];

            return -1;
        }
    }
}
