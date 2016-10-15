using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BACNet2AzureService
{
    [DataContract]
    internal class TelemetryMessage
    {
        [DataMember]
        internal string DeviceId;  // KNX Gateway

        [DataMember]
        internal string MessageSource;  // KNX Source address (GA)

        [DataMember]
        internal string DataType;  // KNX Source address (GA)

        [DataMember]
        internal string TextualValue;

        [DataMember]
        internal DateTime LocalTimeStamp;
    }

}
