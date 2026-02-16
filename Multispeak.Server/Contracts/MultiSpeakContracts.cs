
using System.ServiceModel;
using System.Xml.Serialization;

namespace Multispeak.Server.Contracts;

public static class MultiSpeakNs
{
    public const string MultiSpeakNamespace = "http://www.multispeak.org/Version_4.1_Release";
}
#region Header
[XmlType(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MultiSpeakMsgHeader
{
    [XmlAttribute] public int MajorVersion { get; set; }
    [XmlAttribute] public int MinorVersion { get; set; }
    [XmlAttribute] public int Build { get; set; }
    [XmlAttribute] public string BuildString { get; set; } = "Release";
    [XmlAttribute] public string? UserID { get; set; }
    [XmlAttribute] public string? Pwd { get; set; }
    [XmlAttribute] public string? Company { get; set; }
    [XmlAttribute] public DateTime TimeStamp { get; set; }
}
#endregion

#region Common types

[XmlType(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfErrorObject
{
    public List<ErrorObject>? errorObject { get; set; }
}

[XmlType("errorObject", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ErrorObject
{
    [XmlAttribute] public string? objectID { get; set; }
    [XmlAttribute] public string? errorString { get; set; }
    [XmlAttribute] public string? nounType { get; set; }
    [XmlAttribute] public DateTime eventTime { get; set; }
    [XmlText] public string? Value { get; set; }
}

[XmlType(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfString
{
    [XmlElement("string")]
    public List<string?>? @string { get; set; }
}

[XmlType("meterID", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterId
{
    [XmlAttribute] public string? meterNo { get; set; }
    [XmlAttribute] public string? serviceType { get; set; }
    [XmlAttribute] public string? objectID { get; set; }
    [XmlAttribute] public string? utility { get; set; }
    [XmlText] public string? Value { get; set; }
}

[XmlType(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfMeterID1
{
    [XmlElement("meterID")]
    public List<MeterId?>? meterID { get; set; }
}

[XmlType("expirationTime", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ExpirationTime
{
    [XmlAttribute] public string? units { get; set; }
    [XmlText] public string? Value { get; set; }
}

#endregion

#region PingURL

[MessageContract(WrapperName = "PingURL", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class PingURLRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class PingURLResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? PingURLResult { get; set; }
}
#endregion

#region GetMethods

[MessageContract(WrapperName = "GetMethods", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class GetMethodsRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }
}

[MessageContract(WrapperNamespace= MultiSpeakNs.MultiSpeakNamespace)]
public class GetMethodsResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfString? GetMethodsResult { get; set; }
}

#endregion

#region InitiateOutageDetectionEventRequest

[MessageContract(WrapperName = "InitiateOutageDetectionEventRequest", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class InitiateOutageDetectionEventRequestRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfMeterID1? meterIDs { get; set; }

    [MessageBodyMember]
    public DateTime requestDate { get; set; }

    [MessageBodyMember]
    public string? responseURL { get; set; }

    [MessageBodyMember]
    public string? transactionID { get; set; }

    [MessageBodyMember]
    public ExpirationTime? expTime { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class InitiateOutageDetectionEventRequestResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? InitiateOutageDetectionEventRequestResult { get; set; }
}
#endregion

#region ODEventNotification

[MessageContract(WrapperName = "ODEventNotification", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ODEventNotificationRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfOutageDetectionEvent? ODEvents { get; set; }

    [MessageBodyMember]
    public string? transactionID { get; set; }
}


[XmlType(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfOutageDetectionEvent
{
    [XmlElement("outageDetectionEvent")]
    public List<OutageDetectionEvent?>? outageDetectionEvent { get; set; }
}

[XmlType("outageDetectionEvent", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class OutageDetectionEvent
{
    [XmlAttribute] public string? objectID { get; set; }
    public string? phaseCode { get; set; }
    public DateTime eventTime { get; set; }
    public string? outageEventType { get; set; }
    public string? outageDetectionDeviceID { get; set; }
    public string? comments { get; set; }

    public OutageLocation? outageLocation { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ODEventNotificationResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? ODEventNotificationResult { get; set; }
}
#endregion


[XmlType("outageLocation", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class OutageLocation
{
    [XmlAttribute] public string? objectID { get; set; }
    public string? serviceLocationID { get; set; }
    public MeterId? meterID { get; set; }
    public string? accountNumber { get; set; }
}

#region InitiateMeterReadingsByMeterID

[MessageContract(WrapperName = "InitiateMeterReadingsByMeterID", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class InitiateMeterReadingsByMeterID
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfMeterID1? meterIDs { get; set; }
    
    [MessageBodyMember]
    public string? responseURL { get; set; }
    
    [MessageBodyMember]
    public string? transactionID { get; set; }

    [MessageBodyMember]
    public ExpirationTime? expTime { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class InitiateMeterReadingsByMeterIDResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? InitiateMeterReadingsByMeterIDResult { get; set; }
}
#endregion

#region ReadingChangedNotification (meter readings)
[MessageContract(WrapperName = "ReadingChangedNotification", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ReadingChangedNotificationRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfMeterReading1? changedMeterReads { get; set; }
    [MessageBodyMember]
    public string? transactionID { get; set; }
}

[XmlType("ArrayOfMeterReading1", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfMeterReading1
{
    [XmlElement("meterReading")]
    public List<MeterReading?>? meterReading { get; set; }
}

[XmlType("meterReading", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterReading
{
    [XmlAttribute] public string? objectID { get; set; }
    public MeterId? meterID { get; set; }
    public string? deviceID { get; set; }
    public ArrayOfReadingValue? readingValues { get; set; }
}

[XmlType("ArrayOfReadingValue", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfReadingValue
{
    [XmlElement("readingValue")]
    public List<ReadingValue>? readingValue { get; set; }
}

[XmlType("readingValue", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ReadingValue
{
    public string? units { get; set; }
    public string? value { get; set; }
    public string? ratePeriod { get; set; }
    public string? readingType { get; set; }
    public string? fieldName { get; set; }
    public string? name { get; set; }
    public DateTime timeStamp { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ReadingChangedNotificationResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? ReadingChangedNotificationResult { get; set; }
}
#endregion

#region MeterEventNotification
[MessageContract(WrapperName = "MeterEventNotification", WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterEventNotificationRequest
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public MeterEventList? events { get; set; }

    [MessageBodyMember]
    public string? transactionID { get; set; }
}

[XmlType("meterEventList", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterEventList
{
    public ArrayOfEventInstance? eventInstances { get; set; }
}

[XmlType("ArrayOfEventInstance", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class ArrayOfEventInstance
{
    [XmlElement("eventInstance")]
    public List<EventInstance>? eventInstance { get; set; }
}

[XmlType("eventInstance", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class EventInstance
{
    public MeterId? meterID { get; set; }
    public MeterEvent? meterEvent { get; set; }
    public DateTime timeStamp { get; set; }
}

[XmlType("meterEvent", Namespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterEvent
{
    [XmlAttribute] public string? type { get; set; }
    [XmlAttribute] public string? value { get; set; }
    [XmlText] public string? Value { get; set; }
}

[MessageContract(WrapperNamespace = MultiSpeakNs.MultiSpeakNamespace)]
public class MeterEventNotificationResponse
{
    [MessageHeader]
    public MultiSpeakMsgHeader? MultiSpeakMsgHeader { get; set; }

    [MessageBodyMember]
    public ArrayOfErrorObject? MeterEventNotificationResult { get; set; }
}
#endregion

#region Service contract
[ServiceContract(Namespace = MultiSpeakNs.MultiSpeakNamespace)]
[XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
public interface IOMS_MultiSpeak_v41_Soap
{
    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/PingURL", ReplyAction = "*")]
    PingURLResponse PingURL(PingURLRequest request);

    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/GetMethods", ReplyAction = "*")]
    GetMethodsResponse GetMethods(GetMethodsRequest request);
    
    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/InitiateOutageDetectionEventRequest", ReplyAction = "*")]
    InitiateOutageDetectionEventRequestResponse InitiateOutageDetectionEventRequest(InitiateOutageDetectionEventRequestRequest request);

    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/ODEventNotification", ReplyAction = "*")]
    ODEventNotificationResponse ODEventNotification(ODEventNotificationRequest request);

    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/InitiateMeterReadingsByMeterID", ReplyAction = "*")]
    InitiateMeterReadingsByMeterIDResponse InitiateMeterReadingsByMeterID(InitiateMeterReadingsByMeterID request);

    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/ReadingChangedNotification", ReplyAction = "*")]
    ReadingChangedNotificationResponse ReadingChangedNotification(ReadingChangedNotificationRequest request);

    [OperationContract(Action = MultiSpeakNs.MultiSpeakNamespace + "/MeterEventNotification", ReplyAction = "*")]
    MeterEventNotificationResponse MeterEventNotification(MeterEventNotificationRequest request);

}
#endregion
