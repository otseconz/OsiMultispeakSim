namespace MultiSpeak.Server
{
    public interface IMultiSpeakPlugin
    {
        public void Setup(WebApplicationBuilder builder);

        public void BeforeRun(WebApplication app);
    }
}
