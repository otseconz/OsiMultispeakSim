using Microsoft.Extensions.Options;
using MultiSpeak.Server.Configuration;
using MultiSpeak.Server.Contracts;
using MultiSpeak.Server.Models;
using System.Collections.Concurrent;

namespace MultiSpeak.Server.Services;

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

    private Dictionary<string, IMultiSpeakRequestHandler> _handlers = new();


    public void RegisterHandler(string provider, IMultiSpeakRequestHandler handler)
    {
        _handlers.Add(provider, handler);
    }

    private IMultiSpeakRequestHandler? GetHandler(string? provider)
    {
        if (provider != null && _handlers.TryGetValue(provider, out var handler))
            return handler;
        _handlers.TryGetValue("*", out handler);
        return handler;
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
        ConcurrentDictionary<IMultiSpeakRequestHandler, List<Meter>> providers = new();

        //loop through the meters in the request, find the provider for each meter, and group them by provider so we can send one request per provider.
        //If a meter doesn't have a provider, we'll use the default handler (if it exists)
        foreach (var m in request.meterIDs?.meterID ?? Enumerable.Empty<MeterId?>())
        {
            if (m?.meterNo != null)
            {
                var meter = _meterStore.GetById(m.meterNo);
                if (meter != null)
                {
                    var handler = GetHandler(meter.Provider);
                    if (handler != null)
                    {
                        if (!providers.ContainsKey(handler))
                            providers[handler] = new List<Meter>();
                        providers[handler].Add(meter);
                    }

                    //else
                    //TODO: add an error here?
                }
            }
        }

        //Send the requests to each provider in parallel, and collect the results.
        Task.Run(() =>
        {
            var events = new List<OutageDetectionEvent?>();

            //TODO: we probably need a timeout here, we don't want to wait forever for all the providers to respond
            //TODO: is it allowable to respond in multiple payloads??  Then we don't have to aggregate?  To investigate
            Parallel.ForEach(providers, kvp =>
            {
                events.AddRange(kvp.Key.InitiateOutageDetectionEventRequest(kvp.Value));
            });
            
            var notification = new ODEventNotificationRequest
            {
                ODEvents = new ArrayOfOutageDetectionEvent { outageDetectionEvent = events },
                transactionID = request.transactionID
            };

            //TODO: we should log the response somewhere?
            _client.ODEventNotification(notification);
        });

        return new InitiateOutageDetectionEventRequestResponse
        {
            InitiateOutageDetectionEventRequestResult = new ArrayOfErrorObject { errorObject = errors }
        };
    }

    public InitiateMeterReadingsByMeterIDResponse InitiateMeterReadingsByMeterID(InitiateMeterReadingsByMeterID request)
    {
        var errors = new List<ErrorObject>();
        ConcurrentDictionary<IMultiSpeakRequestHandler, List<Meter>> providers = new();
        
        //loop through the meters in the request, find the provider for each meter, and group them by provider so we can send one request per provider.
        //If a meter doesn't have a provider, we'll use the default handler (if it exists)
        foreach (var m in request.meterIDs?.meterID ?? Enumerable.Empty<MeterId?>())
        {
            if (m?.meterNo != null)
            {
                var meter = _meterStore.GetById(m.meterNo);
                if (meter != null)
                {
                    var handler = GetHandler(meter.Provider);
                    if (handler != null)
                    {
                        if (!providers.ContainsKey(handler))
                            providers[handler] = new List<Meter>();
                        providers[handler].Add(meter);
                    }

                    //else
                    //TODO: add an error here?
                }
            }
        }

        //Send the requests to each provider in parallel, and collect the results.
        Task.Run(() =>
        {
            var events = new List<MeterReading?>();

            //TODO: we probably need a timeout here, we don't want to wait forever for all the providers to respond
            //TODO: is it allowable to respond in multiple payloads??  Then we don't have to aggregate?  To investigate
            Parallel.ForEach(providers, kvp =>
            {
                events.AddRange(kvp.Key.InitiateMeterReadingsByMeterID(kvp.Value));
            });

            var readings = new ReadingChangedNotificationRequest()
            {
                changedMeterReads = new ArrayOfMeterReading1() { meterReading = events },
                transactionID = request.transactionID
            };

            //TODO: we should log the response somewhere?
            _client.ReadingChangedNotification(readings);
        });

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

    #region Unsolocited requests
    //these are used both by the simulator plugin, and also directly from the API

    public void InitiateOutageDetectionEventRequest(string meterId)
    {
        var meter = _meterStore.GetById(meterId);
        if (meter == null)
            return;
       
        var events = InitiateOutageDetectionEventRequest([meter]);
        var notification = new ODEventNotificationRequest
        {
            ODEvents = new ArrayOfOutageDetectionEvent { outageDetectionEvent = events },
            transactionID = Guid.NewGuid().ToString("N")
        };
        _client.ODEventNotification(notification);
    }

    public List<OutageDetectionEvent> InitiateOutageDetectionEventRequest(List<Meter> meters)
    {
        var eventTime = DateTime.UtcNow;

        return meters.Select(meter => new OutageDetectionEvent
        {
            eventTime = eventTime,
            outageEventType = meter.IsOnline ? "Restoration" : "Outage",
            phaseCode = meter.Phases ?? "ABC",
            outageLocation = new OutageLocation()
            {
                meterID = new MeterId()
                {
                    meterNo = meter.Icp,
                    serviceType = "Electric"
                }
            }
        }).ToList();
    }

    public void InitiateMeterReadingsByMeterID(string meterId)
    {
        var meter = _meterStore.GetById(meterId);
        if (meter == null)
            return;

        var readings = InitiateMeterReadingsByMeterID([meter]);
        var notification = new ReadingChangedNotificationRequest()
        {
            changedMeterReads = new ArrayOfMeterReading1() { meterReading = readings },
            transactionID = Guid.NewGuid().ToString("N")
        };
        _client.ReadingChangedNotification(notification);
    }

    public List<MeterReading> InitiateMeterReadingsByMeterID(List<Meter> meters)
    {
        return meters.Select(meter => MeterToReading(meter)).ToList();
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
            objectID = meter.Icp,
            meterID = new MeterId
            {
                Value = meter.Icp,
                meterNo = meter.Icp,
                objectID = meter.Id,
                serviceType = meter.ServiceType,
                utility = meter.Utility
            },
            deviceID = meter.Icp,
            readingValues = new ArrayOfReadingValue { readingValue = rv }
        };
    }

    public void InitiateMeterEventRequest(string meterId)
    {
        var meter = _meterStore.GetById(meterId);
        if (meter == null)
            return;

        var events = InitiateMeterEventRequest([meter]);
        var notification = new MeterEventNotificationRequest()
        {
            events = new MeterEventList()
            {
                eventInstances = new ArrayOfEventInstance()
                {
                    eventInstance = events
                }
            },
            transactionID = Guid.NewGuid().ToString("N")
        };
        _client.MeterEventNotification(notification);
    }

    public List<EventInstance> InitiateMeterEventRequest(List<Meter> meters)
    {
        return meters.Select(meter => new EventInstance
        {
            meterID = new MeterId()
            {
                meterNo = meter.Icp,
                serviceType = meter.ServiceType
            },
            meterEvent = new MeterEvent()
            {
                type = "Status",
                value = meter.CommsStatus ? "Online" : "Offline",
            },
            timeStamp = DateTime.UtcNow
        }).ToList();
    }

#endregion
}
