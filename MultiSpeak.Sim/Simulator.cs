using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiSpeak.Server;
using MultiSpeak.Server.Contracts;
using MultiSpeak.Server.Models;
using MultiSpeak.Server.Services;

namespace MultiSpeak.Sim
{

    public class SimulatorPlugin : IMultiSpeakPlugin
    {
        public void Setup(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<Simulator>();
        }

        public void BeforeRun(WebApplication app)
        {
            //TODO: there must be a better way to do this
            //force the simulator to be instantiated so that the handler gets registered
            app.Services.GetRequiredService<Simulator>();
        }
    }

    /// <summary>
    /// The simulator provider implements the MultiSpeak request handler interface and provides
    /// simulated responses based on the current state of the meters in the MeterStore.
    /// It is registered as the default handler.
    /// </summary>
    public class Simulator : IMultiSpeakRequestHandler
    {
        private readonly ILogger<Simulator> _logger;
        private readonly MultiSpeakService _service;
        
        public Simulator(IHttpContextAccessor httpContextAccessor, ILogger<Simulator> logger, MultiSpeakService service)
        {
            _logger = logger;
            _service = service;
            _service.RegisterHandler("*", this);
        }

        public List<OutageDetectionEvent> InitiateOutageDetectionEventRequest(List<Meter> meters)
        {
            return _service.InitiateOutageDetectionEventRequest(meters);
        }

        public List<MeterReading> InitiateMeterReadingsByMeterID(List<Meter> meters)
        {
            return _service.InitiateMeterReadingsByMeterID(meters);
        }
    }
}
