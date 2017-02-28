namespace KNXLibPortableLib.Utils
{
    using System;
    using System.Net;

    public static class DatagramProcessing
    {

        public static ResponseType ProcessDatagram(byte[] datagram, bool threeLevelGroupAddressing, bool isDebug, out KnxDatagram knxDatagram)
        {
            knxDatagram = new KnxDatagram
            {
                HeaderLength = datagram[0],
                ProtocolVersion = datagram[1],
                ServiceType = new[] { datagram[2], datagram[3] },
                TotalLength = datagram[4] + datagram[5]
            };

            var cemi = new byte[datagram.Length - 6];
            Array.Copy(datagram, 6, cemi, 0, datagram.Length - 6);

            return ProcessCEMI(ref knxDatagram, cemi, threeLevelGroupAddressing, isDebug);
        }

        public static ResponseType ProcessCEMI(ref KnxDatagram datagram, byte[] cemi, bool threeLevelGroupAddressing, bool isDebug)
        {
            try
            {
                // CEMI
                // +--------+--------+--------+--------+----------------+----------------+--------+----------------+
                // |  Msg   |Add.Info| Ctrl 1 | Ctrl 2 | Source Address | Dest. Address  |  Data  |      APDU      |
                // | Code   | Length |        |        |                |                | Length |                |
                // +--------+--------+--------+--------+----------------+----------------+--------+----------------+
                //   1 byte   1 byte   1 byte   1 byte      2 bytes          2 bytes       1 byte      2 bytes
                //
                //  Message Code    = 0x11 - a L_Data.req primitive
                //      COMMON EMI MESSAGE CODES FOR DATA LINK LAYER PRIMITIVES
                //          FROM NETWORK LAYER TO DATA LINK LAYER
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          | Data Link Layer Primitive | Message Code | Data Link Layer Service | Service Description | Common EMI Frame |
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          |        L_Raw.req          |    0x10      |                         |                     |                  |
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          |                           |              |                         | Primitive used for  | Sample Common    |
                //          |        L_Data.req         |    0x11      |      Data Service       | transmitting a data | EMI frame        |
                //          |                           |              |                         | frame               |                  |
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          |        L_Poll_Data.req    |    0x13      |    Poll Data Service    |                     |                  |
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          |        L_Raw.req          |    0x10      |                         |                     |                  |
                //          +---------------------------+--------------+-------------------------+---------------------+------------------+
                //          FROM DATA LINK LAYER TO NETWORK LAYER
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          | Data Link Layer Primitive | Message Code | Data Link Layer Service | Service Description |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |        L_Poll_Data.con    |    0x25      |    Poll Data Service    |                     |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |                           |              |                         | Primitive used for  |
                //          |        L_Data.ind         |    0x29      |      Data Service       | receiving a data    |
                //          |                           |              |                         | frame               |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |        L_Busmon.ind       |    0x2B      |   Bus Monitor Service   |                     |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |        L_Raw.ind          |    0x2D      |                         |                     |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |                           |              |                         | Primitive used for  |
                //          |                           |              |                         | local confirmation  |
                //          |        L_Data.con         |    0x2E      |      Data Service       | that a frame was    |
                //          |                           |              |                         | sent (does not mean |
                //          |                           |              |                         | successful receive) |
                //          +---------------------------+--------------+-------------------------+---------------------+
                //          |        L_Raw.con          |    0x2F      |                         |                     |
                //          +---------------------------+--------------+-------------------------+---------------------+

                //  Add.Info Length = 0x00 - no additional info
                //  Control Field 1 = see the bit structure above
                //  Control Field 2 = see the bit structure above
                //  Source Address  = 0x0000 - filled in by router/gateway with its source address which is
                //                    part of the KNX subnet
                //  Dest. Address   = KNX group or individual address (2 byte)
                //  Data Length     = Number of bytes of data in the APDU excluding the TPCI/APCI bits
                //  APDU            = Application Protocol Data Unit - the actual payload including transport
                //                    protocol control information (TPCI), application protocol control
                //                    information (APCI) and data passed as an argument from higher layers of
                //                    the KNX communication stack
                //
                datagram.MessageCode = cemi[0];
                datagram.AdditionalInfoLength = cemi[1];

                if (datagram.AdditionalInfoLength > 0)
                {
                    datagram.AdditionalInfo = new byte[datagram.AdditionalInfoLength];
                    for (var i = 0; i < datagram.AdditionalInfoLength; i++)
                    {
                        datagram.AdditionalInfo[i] = cemi[2 + i];
                    }
                }

                datagram.ControlField1 = cemi[2 + datagram.AdditionalInfoLength];
                datagram.ControlField2 = cemi[3 + datagram.AdditionalInfoLength];
                datagram.SourceAddress = Networking.GetIndividualAddress(new[] { cemi[4 + datagram.AdditionalInfoLength], cemi[5 + datagram.AdditionalInfoLength] });

                datagram.DestinationAddress =
                    Networking.GetKnxDestinationAddressType(datagram.ControlField2).Equals(KnxDestinationAddressType.Individual)
                        ? Networking.GetIndividualAddress(new[] { cemi[6 + datagram.AdditionalInfoLength], cemi[7 + datagram.AdditionalInfoLength] })
                        : Networking.GetGroupAddress(new[] { cemi[6 + datagram.AdditionalInfoLength], cemi[7 + datagram.AdditionalInfoLength] }, threeLevelGroupAddressing);

                datagram.DataLength = cemi[8 + datagram.AdditionalInfoLength];
                datagram.Apdu = new byte[datagram.DataLength + 1];

                for (var i = 0; i < datagram.Apdu.Length; i++)
                    datagram.Apdu[i] = cemi[9 + i + datagram.AdditionalInfoLength];

                datagram.Data = DataProcessing.GetData(datagram.DataLength, datagram.Apdu);

                if (isDebug)
                {
                    System.Diagnostics.Debug.WriteLine("-----------------------------------------------------------------------------------------------------");
                    System.Diagnostics.Debug.WriteLine(BitConverter.ToString(cemi));
                    System.Diagnostics.Debug.WriteLine("Event Header Length: " + datagram.HeaderLength);
                    System.Diagnostics.Debug.WriteLine("Event Protocol Version: " + datagram.ProtocolVersion.ToString("x"));
                    System.Diagnostics.Debug.WriteLine("Event Service Type: 0x" + BitConverter.ToString(datagram.ServiceType).Replace("-", string.Empty));
                    System.Diagnostics.Debug.WriteLine("Event Total Length: " + datagram.TotalLength);

                    System.Diagnostics.Debug.WriteLine("Event Message Code: " + datagram.MessageCode.ToString("x"));
                    System.Diagnostics.Debug.WriteLine("Event Aditional Info Length: " + datagram.AdditionalInfoLength);

                    if (datagram.AdditionalInfoLength > 0)
                        System.Diagnostics.Debug.WriteLine("Event Aditional Info: 0x" + BitConverter.ToString(datagram.AdditionalInfo).Replace("-", string.Empty));

                    System.Diagnostics.Debug.WriteLine("Event Control Field 1: " + Convert.ToString(datagram.ControlField1, 2));
                    System.Diagnostics.Debug.WriteLine("Event Control Field 2: " + Convert.ToString(datagram.ControlField2, 2));
                    System.Diagnostics.Debug.WriteLine("Event Source Address: " + datagram.SourceAddress);
                    System.Diagnostics.Debug.WriteLine("Event Destination Address: " + datagram.DestinationAddress);
                    System.Diagnostics.Debug.WriteLine("Event Data Length: " + datagram.DataLength);
                    System.Diagnostics.Debug.WriteLine("Event APDU: 0x" + BitConverter.ToString(datagram.Apdu).Replace("-", string.Empty));
                    System.Diagnostics.Debug.WriteLine("Event Data: " + datagram.Data);
                    System.Diagnostics.Debug.WriteLine("-----------------------------------------------------------------------------------------------------");
                }

                if (datagram.MessageCode != 0x29)
                {
                    return ResponseType.Other;                    
                }

                var type = datagram.Apdu[1] >> 4;

                switch (type)
                {
                    case 8:
                        return ResponseType.Event;
                    case 4:
                        return ResponseType.Status;
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                System.Diagnostics.Debug.WriteLine("Catch exception in datagram.Apdu.Length");
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return ResponseType.Other;
        }

        public static byte[] CreateTunnelingAck(byte sequenceNumber, byte channelId)
        {
            byte[] datagram = new byte[10];
            datagram[00] = 0x06;
            datagram[01] = 0x10;
            datagram[02] = 0x04;
            datagram[03] = 0x21;
            datagram[04] = 0x00;
            datagram[05] = 0x0A;

            datagram[06] = 0x04;
            datagram[07] = channelId;
            datagram[08] = sequenceNumber;
            datagram[09] = 0x00;

            return datagram;
        }

        public static byte[] CreateDisconnectRequest(IPEndPoint localEndpoint, byte channelId)
        {
            byte[] datagram = new byte[16];
            datagram[00] = 0x06;
            datagram[01] = 0x10;
            datagram[02] = 0x02;
            datagram[03] = 0x09;
            datagram[04] = 0x00;
            datagram[05] = 0x10;

            datagram[06] = channelId;
            datagram[07] = 0x00;
            datagram[08] = 0x08;
            datagram[09] = 0x01;
            datagram[10] = localEndpoint.Address.GetAddressBytes()[0];
            datagram[11] = localEndpoint.Address.GetAddressBytes()[1];
            datagram[12] = localEndpoint.Address.GetAddressBytes()[2];
            datagram[13] = localEndpoint.Address.GetAddressBytes()[3];
            datagram[14] = (byte)(localEndpoint.Port >> 8);
            datagram[15] = (byte)localEndpoint.Port;

            return datagram;
        }

        public static byte[] CreateConnectRequest(IPEndPoint localEndpoint)
        {
            byte[] datagram = new byte[26];
            datagram[00] = 0x06;
            datagram[01] = 0x10;
            datagram[02] = 0x02;
            datagram[03] = 0x05;
            datagram[04] = 0x00;
            datagram[05] = 0x1A;

            datagram[06] = 0x08;
            datagram[07] = 0x01;
            datagram[08] = localEndpoint.Address.GetAddressBytes()[0];
            datagram[09] = localEndpoint.Address.GetAddressBytes()[1];
            datagram[10] = localEndpoint.Address.GetAddressBytes()[2];
            datagram[11] = localEndpoint.Address.GetAddressBytes()[3];
            datagram[12] = (byte)(localEndpoint.Port >> 8);
            datagram[13] = (byte)localEndpoint.Port;
            datagram[14] = 0x08;
            datagram[15] = 0x01;
            datagram[16] = localEndpoint.Address.GetAddressBytes()[0];
            datagram[17] = localEndpoint.Address.GetAddressBytes()[1];
            datagram[18] = localEndpoint.Address.GetAddressBytes()[2];
            datagram[19] = localEndpoint.Address.GetAddressBytes()[3];
            datagram[20] = (byte)(localEndpoint.Port >> 8);
            datagram[21] = (byte)localEndpoint.Port;
            datagram[22] = 0x04;
            datagram[23] = 0x04;
            datagram[24] = 0x02;
            datagram[25] = 0x00;

            return datagram;
        }

        public static byte[] CreateStateRequest(IPEndPoint localEndpoint, byte channelId)
        {
            byte[] datagram = new byte[16];
            datagram[00] = 0x06;
            datagram[01] = 0x10;
            datagram[02] = 0x02;
            datagram[03] = 0x07;
            datagram[04] = 0x00;
            datagram[05] = 0x10;

            datagram[06] = channelId;
            datagram[07] = 0x00;
            datagram[08] = 0x08;
            datagram[09] = 0x01;
            datagram[10] = localEndpoint.Address.GetAddressBytes()[0];
            datagram[11] = localEndpoint.Address.GetAddressBytes()[1];
            datagram[12] = localEndpoint.Address.GetAddressBytes()[2];
            datagram[13] = localEndpoint.Address.GetAddressBytes()[3];
            datagram[14] = (byte)(localEndpoint.Port >> 8);
            datagram[15] = (byte)localEndpoint.Port;
            return datagram;
        }

        public static byte[] CreateActionDatagramCommon(string destinationAddress, byte[] data, byte[] header, byte actionMessageCode)
        {
            int i;
            var dataLength = DataProcessing.GetDataLength(data);

            // HEADER
            var datagram = new byte[dataLength + 10 + header.Length];
            for (i = 0; i < header.Length; i++)
                datagram[i] = header[i];

            // CEMI (start at position 6)
            // +--------+--------+--------+--------+----------------+----------------+--------+----------------+
            // |  Msg   |Add.Info| Ctrl 1 | Ctrl 2 | Source Address | Dest. Address  |  Data  |      APDU      |
            // | Code   | Length |        |        |                |                | Length |                |
            // +--------+--------+--------+--------+----------------+----------------+--------+----------------+
            //   1 byte   1 byte   1 byte   1 byte      2 bytes          2 bytes       1 byte      2 bytes
            //
            //  Message Code    = 0x11 - a L_Data.req primitive
            //      COMMON EMI MESSAGE CODES FOR DATA LINK LAYER PRIMITIVES
            //          FROM NETWORK LAYER TO DATA LINK LAYER
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          | Data Link Layer Primitive | Message Code | Data Link Layer Service | Service Description | Common EMI Frame |
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          |        L_Raw.req          |    0x10      |                         |                     |                  |
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          |                           |              |                         | Primitive used for  | Sample Common    |
            //          |        L_Data.req         |    0x11      |      Data Service       | transmitting a data | EMI frame        |
            //          |                           |              |                         | frame               |                  |
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          |        L_Poll_Data.req    |    0x13      |    Poll Data Service    |                     |                  |
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          |        L_Raw.req          |    0x10      |                         |                     |                  |
            //          +---------------------------+--------------+-------------------------+---------------------+------------------+
            //          FROM DATA LINK LAYER TO NETWORK LAYER
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          | Data Link Layer Primitive | Message Code | Data Link Layer Service | Service Description |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |        L_Poll_Data.con    |    0x25      |    Poll Data Service    |                     |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |                           |              |                         | Primitive used for  |
            //          |        L_Data.ind         |    0x29      |      Data Service       | receiving a data    |
            //          |                           |              |                         | frame               |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |        L_Busmon.ind       |    0x2B      |   Bus Monitor Service   |                     |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |        L_Raw.ind          |    0x2D      |                         |                     |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |                           |              |                         | Primitive used for  |
            //          |                           |              |                         | local confirmation  |
            //          |        L_Data.con         |    0x2E      |      Data Service       | that a frame was    |
            //          |                           |              |                         | sent (does not mean |
            //          |                           |              |                         | successful receive) |
            //          +---------------------------+--------------+-------------------------+---------------------+
            //          |        L_Raw.con          |    0x2F      |                         |                     |
            //          +---------------------------+--------------+-------------------------+---------------------+

            //  Add.Info Length = 0x00 - no additional info
            //  Control Field 1 = see the bit structure above
            //  Control Field 2 = see the bit structure above
            //  Source Address  = 0x0000 - filled in by router/gateway with its source address which is
            //                    part of the KNX subnet
            //  Dest. Address   = KNX group or individual address (2 byte)
            //  Data Length     = Number of bytes of data in the APDU excluding the TPCI/APCI bits
            //  APDU            = Application Protocol Data Unit - the actual payload including transport
            //                    protocol control information (TPCI), application protocol control
            //                    information (APCI) and data passed as an argument from higher layers of
            //                    the KNX communication stack
            //

            datagram[i++] =
                actionMessageCode != 0x00
                    ? actionMessageCode
                    : (byte)0x11;

            datagram[i++] = 0x00;
            datagram[i++] = 0xAC;

            datagram[i++] =
                Networking.IsAddressIndividual(destinationAddress)
                    ? (byte)0x50
                    : (byte)0xF0;

            datagram[i++] = 0x00;
            datagram[i++] = 0x00;
            var dst_address = Networking.GetAddress(destinationAddress);
            datagram[i++] = dst_address[0];
            datagram[i++] = dst_address[1];
            datagram[i++] = (byte)dataLength;
            datagram[i++] = 0x00;
            datagram[i] = 0x80;

            DataProcessing.WriteData(datagram, data, i);

            return datagram;
        }

        public static byte[] CreateRequestStatusDatagramCommon(string destinationAddress, byte[] datagram, int cemi_start_pos, byte actionMessageCode)
        {
            var i = 0;

            datagram[cemi_start_pos + i++] =
                actionMessageCode != 0x00
                    ? actionMessageCode
                    : (byte)0x11;

            datagram[cemi_start_pos + i++] = 0x00;
            datagram[cemi_start_pos + i++] = 0xAC;

            datagram[cemi_start_pos + i++] =
                Networking.IsAddressIndividual(destinationAddress)
                    ? (byte)0x50
                    : (byte)0xF0;

            datagram[cemi_start_pos + i++] = 0x00;
            datagram[cemi_start_pos + i++] = 0x00;
            byte[] dst_address = Networking.GetAddress(destinationAddress);
            datagram[cemi_start_pos + i++] = dst_address[0];
            datagram[cemi_start_pos + i++] = dst_address[1];

            datagram[cemi_start_pos + i++] = 0x01;
            datagram[cemi_start_pos + i++] = 0x00;
            datagram[cemi_start_pos + i] = 0x00;

            return datagram;
        }
    }
}
