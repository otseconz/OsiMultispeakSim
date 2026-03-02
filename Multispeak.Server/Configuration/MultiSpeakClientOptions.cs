namespace MultiSpeak.Server.Configuration;

/// <summary>
/// Configuration for the "other end" (client) when the simulator sends callback requests
/// (e.g. ODEventNotification after InitiateOutageDetectionEventRequest).
/// MultiSpeak username/password are used for HTTP Basic authorization on outbound callbacks.
/// </summary>
public class MultiSpeakClientOptions
{
    public const string SectionName = "MultiSpeakClient";

    /// <summary>Base URL of the client MultiSpeak endpoint (e.g. https://client.example.com:58310). Used when the incoming request does not provide responseURL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>MultiSpeak user name for HTTP Basic auth on callback requests.</summary>
    public string? Username { get; set; }

    /// <summary>MultiSpeak password for HTTP Basic auth on callback requests.</summary>
    public string? Password { get; set; }

    /// <summary>Company name sent in MultiSpeakMsgHeader on outbound callbacks (protocol-level).</summary>
    public string? Company { get; set; }

    /// <summary>Path to the MultiSpeak SOAP endpoint relative to BaseUrl (default /MultiSpeak).</summary>
    public string? MultiSpeakPath { get; set; }

}
