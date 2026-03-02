
using Microsoft.Extensions.Options;
using MultiSpeak.Server.Contracts;
using System.ServiceModel;
using MultiSpeak.Server.Configuration;

namespace MultiSpeak.Server.Services;

public class MultiSpeakClient(ChannelFactory<IOMS_MultiSpeak_v41_Soap> factory, IOptions<MultiSpeakClientOptions> options)
{
    private MultiSpeakMsgHeader _header = new MultiSpeakMsgHeader() {
        Build = 3,
        UserID = options.Value.Username,
        Pwd = options.Value.Password,
        BuildString = "Release",
        MajorVersion = 4,
        MinorVersion = 1,
        TimeStamp = DateTime.Now };//TODO: not sure exactly what the purpose of this is?

    public GetMethodsResponse GetMethods(GetMethodsRequest request)
    {
        var channel = factory.CreateChannel();
        try
        {
            var result = channel.GetMethods(request);
            
            ((IClientChannel)channel).Close();
            return result;
        }
        catch
        {
            ((IClientChannel)channel).Abort();
            throw;
        }
    }

    public ODEventNotificationResponse ODEventNotification(ODEventNotificationRequest request)
    {
        var channel = factory.CreateChannel();
        try
        {
            request.MultiSpeakMsgHeader = _header;
            var result = channel.ODEventNotification(request);
            ((IClientChannel)channel).Close();
            return result;
        }
        catch
        {
            ((IClientChannel)channel).Abort();
            throw;
        }
    }

    public ReadingChangedNotificationResponse ReadingChangedNotification(ReadingChangedNotificationRequest request)
    {
        var channel = factory.CreateChannel();
        try
        {
            request.MultiSpeakMsgHeader = _header;
            var result = channel.ReadingChangedNotification(request);

            ((IClientChannel)channel).Close();
            return result;
        }
        catch
        {
            ((IClientChannel)channel).Abort();
            throw;
        }
    }

    public MeterEventNotificationResponse MeterEventNotification(MeterEventNotificationRequest request)
    {
        var channel = factory.CreateChannel();
        try
        {
            request.MultiSpeakMsgHeader = _header;
            var result = channel.MeterEventNotification(request);

            ((IClientChannel)channel).Close();
            return result;
        }
        catch
        {
            ((IClientChannel)channel).Abort();
            throw;
        }
    }
}