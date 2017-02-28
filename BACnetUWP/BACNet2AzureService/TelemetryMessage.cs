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
        [DataMember] internal string GatewaySource;

        [DataMember] internal string DeviceSource;  

        [DataMember] internal string MessageSource;  

        [DataMember] internal string MessageType;  

        [DataMember] internal string Value;

        [DataMember] internal DateTime GatewayTimeStampUtc; 
    }
        
}
