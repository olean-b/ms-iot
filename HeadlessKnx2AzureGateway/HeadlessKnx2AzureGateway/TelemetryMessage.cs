using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace HeadlessKnx2AzureGateway
{

    [DataContract]
    internal class TelemetryMessage
    {
        [DataMember]
        internal string DeviceId;  // KNX Gateway

        [DataMember]
        internal string KNXMessageSource;  // KNX Source address (GA)

        [DataMember]
        internal string KNXDataType;  // KNX Source address (GA)

        [DataMember]
        internal string KNXTextualValue;

        [DataMember]
        internal DateTime LocalTimeStamp;
    }

}
