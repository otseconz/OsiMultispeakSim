using MultiSpeak.Server.Contracts;
using MultiSpeak.Server.Models;

namespace MultiSpeak.Server.Services;

public interface IMultiSpeakRequestHandler
{
    public List<OutageDetectionEvent> InitiateOutageDetectionEventRequest(List<Meter> meters);


    public List<MeterReading> InitiateMeterReadingsByMeterID(List<Meter> meters );
}