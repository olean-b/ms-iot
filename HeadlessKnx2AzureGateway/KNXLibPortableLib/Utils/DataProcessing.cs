namespace KNXLibPortableLib.Utils
{
    using System;

    public static class DataProcessing
    {
        // In the Common EMI frame, the APDU payload is defined as follows:

        // +--------+--------+--------+--------+--------+
        // | TPCI + | APCI + |  Data  |  Data  |  Data  |
        // |  APCI  |  Data  |        |        |        |
        // +--------+--------+--------+--------+--------+
        //   byte 1   byte 2  byte 3     ...     byte 16

        // For data that is 6 bits or less in length, only the first two bytes are used in a Common EMI
        // frame. Common EMI frame also carries the information of the expected length of the Protocol
        // Data Unit (PDU). Data payload can be at most 14 bytes long.  <p>

        // The first byte is a combination of transport layer control information (TPCI) and application
        // layer control information (APCI). First 6 bits are dedicated for TPCI while the two least
        // significant bits of first byte hold the two most significant bits of APCI field, as follows:

        //   Bit 1    Bit 2    Bit 3    Bit 4    Bit 5    Bit 6    Bit 7    Bit 8      Bit 1   Bit 2
        // +--------+--------+--------+--------+--------+--------+--------+--------++--------+----....
        // |        |        |        |        |        |        |        |        ||        |
        // |  TPCI  |  TPCI  |  TPCI  |  TPCI  |  TPCI  |  TPCI  | APCI   |  APCI  ||  APCI  |
        // |        |        |        |        |        |        |(bit 1) |(bit 2) ||(bit 3) |
        // +--------+--------+--------+--------+--------+--------+--------+--------++--------+----....
        // +                            B  Y  T  E    1                            ||       B Y T E  2
        // +-----------------------------------------------------------------------++-------------....

        //Total number of APCI control bits can be either 4 or 10. The second byte bit structure is as follows:

        //   Bit 1    Bit 2    Bit 3    Bit 4    Bit 5    Bit 6    Bit 7    Bit 8      Bit 1   Bit 2
        // +--------+--------+--------+--------+--------+--------+--------+--------++--------+----....
        // |        |        |        |        |        |        |        |        ||        |
        // |  APCI  |  APCI  | APCI/  |  APCI/ |  APCI/ |  APCI/ | APCI/  |  APCI/ ||  Data  |  Data
        // |(bit 3) |(bit 4) | Data   |  Data  |  Data  |  Data  | Data   |  Data  ||        |
        // +--------+--------+--------+--------+--------+--------+--------+--------++--------+----....
        // +                            B  Y  T  E    2                            ||       B Y T E  3
        // +-----------------------------------------------------------------------++-------------....
        public static string GetData(int dataLength, byte[] apdu)
        {

            switch (dataLength)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return Convert.ToChar(0x3F & apdu[1]).ToString();
                case 2:
                    return Convert.ToChar(apdu[2]).ToString();
                default:
                    var data = string.Empty;
                    for (var i = 2; i < apdu.Length; i++)
                        data += Convert.ToChar(apdu[i]);

                    return data;
            }
        }

        public static int GetDataLength(byte[] data)
        {
            if (data.Length <= 0)
                return 0;

            if (data.Length == 1 && data[0] < 0x3F)
                return 1;

            if (data[0] < 0x3F)
                return data.Length;

            return data.Length + 1;
        }

        public static void WriteData(byte[] datagram, byte[] data, int dataStart)
        {
            if (data.Length == 1)
            {
                if (data[0] < 0x3F)
                {
                    datagram[dataStart] = (byte)(datagram[dataStart] | data[0]);
                }
                else
                {
                    datagram[dataStart + 1] = data[0];
                }
            }
            else if (data.Length > 1)
            {
                if (data[0] < 0x3F)
                {
                    datagram[dataStart] = (byte)(datagram[dataStart] | data[0]);

                    for (var i = 1; i < data.Length; i++)
                    {
                        datagram[dataStart + i] = data[i];
                    }
                }
                else
                {
                    for (var i = 0; i < data.Length; i++)
                    {
                        datagram[dataStart + 1 + i] = data[i];
                    }
                }
            }
        }
    }
}
