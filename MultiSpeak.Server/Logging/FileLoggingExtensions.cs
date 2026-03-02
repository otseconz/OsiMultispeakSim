using Microsoft.Extensions.Logging;

namespace MultiSpeak.Server.Logging;

public static class FileLoggingExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, Action<FileLoggerOptions>? configure = null)
    {
        var opts = new FileLoggerOptions();
        configure?.Invoke(opts);

        builder.AddProvider(new FileLoggerProvider(opts));
        return builder;
    }
}
