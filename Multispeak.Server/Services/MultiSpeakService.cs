using Microsoft.Extensions.Options;
using Multispeak.Server.Configuration;
using Multispeak.Server.Contracts;
using Multispeak.Server.Models;

namespace Multispeak.Server.Services;

public class MultiSpeakService : IOMS_MultiSpeak_v41_Soap
{
    private static readonly string[] SupportedMethods =
    {
        "PingURL", "GetMethods", "InitiateOutageDetectionEventRequest", "ODEventNotification",
        "GetOutageEventStatusByOutageLocation", "InitiateMeterReadingsByMeterID", "ReadingChangedNotification",
        "MeterEventNotification", "CallBackListNotification", "GetOutageEvent", "OutageEventChangedNotification",
        "CustomersAffectedByOutageNotification"
    };
    private readonly IMeterStore _meterStore;
    private readonly MultiSpeakClientOptions _clientOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MultiSpeakService> _logger;
    private readonly MultiSpeakClient _client;

    public MultiSpeakService(
        IMeterStore meterStore,
        IOptions<MultiSpeakClientOptions> clientOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MultiSpeakService> logger,
            MultiSpeakClient client)
    {
        _meterStore = meterStore;
        
        _clientOptions = clientOptions.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _client = client;

    }

    #region Not used

    public PingURLResponse PingURL(PingURLRequest request) => new()
    {
        PingURLResult = new ArrayOfErrorObject { errorObject = new List<ErrorObject>() }
    };

    public GetMethodsResponse GetMethods(GetMethodsRequest request)
    {
        return new GetMethodsResponse
        {
            GetMethodsResult = new ArrayOfString { @string = SupportedMethods.ToList<string?>() },
        };
    }

    #endregion


    #region Incoming Requests
    public InitiateOutageDetectionEventRequestResponse InitiateOutageDetectionEventRequest(InitiateOutageDetectionEventRequestRequest request)
    {
        var errors = new List<ErrorObject>();
        
        var meterIds = new List<string>();

        foreach (var m in request.meterIDs?.meterID ?? Enumerable.Empty<MeterId?>())
        {
            if (m?.meterNo != null)
            {
                meterIds.Add(m.meterNo);
            }
        }

        _ = SendOdEventNotificationAsync(meterIds, request.transactionID);
        
        return new InitiateOutageDetectionEventRequestResponse
        {
            InitiateOutageDetectionEventRequestResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    public InitiateMeterReadingsByMeterIDResponse InitiateMeterReadingsByMeterID(InitiateMeterReadingsByMeterID request)
    {
        var errors = new List<ErrorObject>();
        var meterIds = new List<string>();

        foreach (var m in request.meterIDs?.meterID ?? Enumerable.Empty<MeterId?>())
        {
            if (m?.meterNo != null)
            {
                meterIds.Add(m.meterNo);
            }
        }
        
        if (meterIds.Count > 0)
            _ = SendReadingChangedAsync(meterIds, request.transactionID);

        return new InitiateMeterReadingsByMeterIDResponse
        {
            InitiateMeterReadingsByMeterIDResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    #endregion

    #region Outgoing Requests
    //we need to include these functions, because we make them back to the client.  They will never actually be called.

    public ODEventNotificationResponse ODEventNotification(ODEventNotificationRequest request)
    {
        var errors = new List<ErrorObject>();
        return new ODEventNotificationResponse
        {
            ODEventNotificationResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    public ReadingChangedNotificationResponse ReadingChangedNotification(ReadingChangedNotificationRequest request)
    {
        var errors = new List<ErrorObject>();
        return new ReadingChangedNotificationResponse
        {
            ReadingChangedNotificationResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    public MeterEventNotificationResponse MeterEventNotification(MeterEventNotificationRequest request)
    {
        var errors = new List<ErrorObject>();
        return new MeterEventNotificationResponse
        {
            MeterEventNotificationResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    #endregion

    #region Async Client Requests
    public Task<ODEventNotificationResponse> SendOdEventNotificationAsync(List<string> meterIds, string? transactionID = null)
    {
        return Task.Run(() =>
        {
            var events = new List<OutageDetectionEvent?>();
            var eventTime = DateTime.UtcNow;
            transactionID ??= Guid.NewGuid().ToString("N");

            if (meterIds.Count > 0)
            {
                foreach (var mid in meterIds)
                {
                    var meter = _meterStore.GetByMeterNo(mid);
                    //TODO: add an error here?
                    if (meter == null)
                        continue;
                    events.Add(new OutageDetectionEvent
                    {
                        eventTime = eventTime,
                        outageEventType = meter.IsOnline ? "Restoration" : "Outage",
                        phaseCode = meter.Phases ?? "ABC",
                        outageLocation = new OutageLocation()
                        {
                            meterID = new MeterId()
                            {
                                meterNo = meter.MeterNo,
                                serviceType = "Electric"
                            }
                        }
                    });
                }
            }

            if (events.Count == 0)
                events.Add(new OutageDetectionEvent { eventTime = eventTime, outageEventType = "Outage", comments = "Simulated outage detection event" });

            var notification = new ODEventNotificationRequest
            {
                ODEvents = new ArrayOfOutageDetectionEvent { outageDetectionEvent = events },
                transactionID = transactionID
            };
            return _client.ODEventNotification(notification);
        });
    }

    public Task<ReadingChangedNotificationResponse?> SendReadingChangedAsync(List<string> meterIds, string? transactionID = null)
    {
        return Task.Run(() =>
        {
            if (meterIds.Count == 0)
            {
                return null;
                //errors.Add(new ErrorObject { errorString = "No meter IDs provided." });
                //return new InitiateMeterReadingsByMeterIDResponse
                //{
                  //  InitiateMeterReadingsByMeterIDResult = new ArrayOfErrorObject { errorObject = errors }
                //};
            }

            var readings = new List<MeterReading?>();
            foreach (var mid in meterIds)
            {
                Meter? meter = _meterStore.GetByMeterNo(mid);

                if (meter == null)
                {
                    //errors.Add(new ErrorObject { objectID = mid.objectID ?? mid.meterNo, errorString = "Meter not found." });
                    //TODO: throw an error
                    continue;
                }

                readings.Add(MeterToReading(meter));
            }

            transactionID ??= Guid.NewGuid().ToString("N");

            var payload = new ReadingChangedNotificationRequest
            {
                changedMeterReads = new ArrayOfMeterReading1 { meterReading = readings },
                transactionID = transactionID
            };

            return _client.ReadingChangedNotification(payload);
        });
    }

    public Task<MeterEventNotificationResponse?> SendMeterEventNotificationAsync(string meterId, string? transcationID = null)
    {
        return Task.Run(() =>
        {
            var meter = _meterStore.GetByMeterNo(meterId);
            if (meter == null)
                return null;
            var request = new MeterEventNotificationRequest()
            {
                events = new MeterEventList()
                {

                    eventInstances = new ArrayOfEventInstance()
                    {
                        eventInstance =
                        [
                            new EventInstance
                            {
                                meterID = new MeterId()
                                {
                                    meterNo = meter.MeterNo,
                                    serviceType = meter.ServiceType
                                },
                                meterEvent = new MeterEvent()
                                {
                                    type = "Status",
                                    value = meter.CommsStatus ? "Normal" : "Non-Responsive",
                                },
                                timeStamp = DateTime.UtcNow
                            }
                        ]
                    }
                },
                transactionID = Guid.NewGuid().ToString("N")
            };

            return _client.MeterEventNotification(request);
        });
    }
    #endregion

    #region Helpers



    /// <summary>Build meter readings for sending ReadingChangedNotification (e.g. from web UI).</summary>
    public MeterReading? GetReadingsForMeter(string meterNo)
    {
        var meter = _meterStore.GetByMeterNo(meterNo);
        return meter != null ? MeterToReading(meter) : null;
    }

    private static MeterReading MeterToReading(Meter meter)
    {
        var rv = new List<ReadingValue>();
        var ts = DateTime.UtcNow;
        if (meter.ActivePowerReading.HasValue)
            rv.Add(new ReadingValue { units = "kW", value = meter.ActivePowerReading.Value.ToString("F2"), name = "ActivePower", timeStamp = ts });
        if (meter.ReactivePowerReading.HasValue)
            rv.Add(new ReadingValue { units = "kVAR", value = meter.ReactivePowerReading.Value.ToString("F2"), name = "ReactivePower", timeStamp = ts });
        if (meter.VoltageReading.HasValue)
            rv.Add(new ReadingValue { units = "V", value = meter.VoltageReading.Value.ToString("F2"), name = "Voltage", timeStamp = ts });
        if (meter.CurrentReading.HasValue)
            rv.Add(new ReadingValue { units = "Amps", value = meter.CurrentReading.Value.ToString("F2"), name = "Current", timeStamp = ts });

        return new MeterReading
        {
            objectID = meter.MeterNo,
            meterID = new MeterId
            {
                Value = meter.MeterNo,
                meterNo = meter.MeterNo,
                objectID = meter.ObjectId,
                serviceType = meter.ServiceType,
                utility = meter.Utility
            },
            deviceID = meter.MeterNo,
            readingValues = new ArrayOfReadingValue { readingValue = rv }
        };
    }

    #endregion
}
