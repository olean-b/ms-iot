namespace KNXLibPortableLib.Utils
{
    public enum ServiceType
    {
        //0x0201
        SearchRequest,
        //0x0202
        SearchResponse,
        //0x0203
        DescriptionRequest,
        //0x0204
        DescriptionResponse,
        //0x0205
        ConnectionRequest,
        //0x0206
        ConnectionResponse,
        //0x0207
        ConnectionStateRequest,
        //0x0208
        ConnectionStateResponse,
        //0x0209
        DisconnectRequest,
        //0x020A
        DisconnectResponse,
        //0x0310
        DeviceConfigurationRequest,
        //0x0311
        DeviceConfigurationAck,
        //0x0420
        TunnelingRequest,
        //0x0421
        TunnelingAck,
        //0x0530
        RoutingIndication,
        //0x0531
        RoutingLostMessage,
        // UNKNOWN
        Unknown
    }


    // Bit order
    // +---+---+---+---+---+---+---+---+
    // | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
    // +---+---+---+---+---+---+---+---+

    //  Control Field 1

    //   Bit  |
    //  ------+---------------------------------------------------------------
    //    7   | Frame Type  - 0x0 for extended frame
    //        |               0x1 for standard frame
    //  ------+---------------------------------------------------------------
    //    6   | Reserved
    //        |
    //  ------+---------------------------------------------------------------
    //    5   | Repeat Flag - 0x0 repeat frame on medium in case of an error
    //        |               0x1 do not repeat
    //  ------+---------------------------------------------------------------
    //    4   | System Broadcast - 0x0 system broadcast
    //        |                    0x1 broadcast
    //  ------+---------------------------------------------------------------
    //    3   | Priority    - 0x0 system
    //        |               0x1 normal (also called alarm priority)
    //  ------+               0x2 urgent (also called high priority)
    //    2   |               0x3 low
    //        |
    //  ------+---------------------------------------------------------------
    //    1   | Acknowledge Request - 0x0 no ACK requested
    //        | (L_Data.req)          0x1 ACK requested
    //  ------+---------------------------------------------------------------
    //    0   | Confirm      - 0x0 no error
    //        | (L_Data.con) - 0x1 error
    //  ------+---------------------------------------------------------------


    //  Control Field 2

    //   Bit  |
    //  ------+---------------------------------------------------------------
    //    7   | Destination Address Type - 0x0 individual address
    //        |                          - 0x1 group address
    //  ------+---------------------------------------------------------------
    //   6-4  | Hop Count (0-7)
    //  ------+---------------------------------------------------------------
    //   3-0  | Extended Frame Format - 0x0 standard frame
    //  ------+---------------------------------------------------------------
    public enum KnxDestinationAddressType
    {
        Individual = 0,
        Group = 1
    }

    public enum ResponseType
    {
        Other = 0,
        Status = 4,
        Event = 8
    }
}
