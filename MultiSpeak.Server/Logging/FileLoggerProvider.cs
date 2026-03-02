using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MultiSpeak.Server.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly string _baseDirectory;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);

    public FileLoggerProvider(FileLoggerOptions options, string? baseDirectory = null)
    {
        _options = options;
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _options, _baseDirectory));

    public void Dispose()
    {
        _loggers.Clear();
    }
}
