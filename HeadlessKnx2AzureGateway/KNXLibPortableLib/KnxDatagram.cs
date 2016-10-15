namespace KNXLibPortableLib
{
    public class KnxDatagram
    {
        // HEADER
        public int HeaderLength { get; set; }
        public byte ProtocolVersion { get; set; }
        public byte[] ServiceType { get; set; }
        public int TotalLength { get; set; }

        // CONNECTION
        public byte ChannelID { get; set; }
        public byte Status { get; set; }

        // CEMI
        public byte MessageCode { get; set; }
        public int AdditionalInfoLength { get; set; }
        public byte[] AdditionalInfo { get; set; }
        public byte ControlField1 { get; set; }
        public byte ControlField2 { get; set; }
        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public int DataLength { get; set; }
        public byte[] Apdu { get; set; }
        public string Data { get; set; }
    }
}
