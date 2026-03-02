using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MultiSpeak.Server.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerOptions _options;
    private readonly string _baseDirectory;
    private static readonly object WriteLock = new();

    public FileLogger(string categoryName, FileLoggerOptions options, string baseDirectory)
    {
        _categoryName = categoryName;
        _options = options;
        _baseDirectory = baseDirectory;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
            return;

        var utcNow = DateTime.UtcNow;
        var line = FormatLine(utcNow, logLevel, eventId, _categoryName, message, exception);

        var logDir = _options.LogDirectory;
        if (!Path.IsPathRooted(logDir))
            logDir = Path.Combine(_baseDirectory, logDir);

        Directory.CreateDirectory(logDir);

        var filePath = Path.Combine(logDir, $"{_options.FileNamePrefix}-{utcNow:yyyyMMdd}.log");

        lock (WriteLock)
        {
            File.AppendAllText(filePath, line, Encoding.UTF8);
        }
    }

    private static string FormatLine(DateTime utcNow, LogLevel level, EventId eventId, string category, string message,
        Exception? ex)
    {
        var sb = new StringBuilder(256);
        sb.Append(utcNow.ToString("O", CultureInfo.InvariantCulture));
        sb.Append(' ');
        sb.Append(level.ToString().ToUpperInvariant());
        sb.Append(" [");
        sb.Append(category);
        sb.Append(']');
        if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
        {
            sb.Append(" (");
            sb.Append(eventId.Id);
            if (!string.IsNullOrWhiteSpace(eventId.Name))
            {
                sb.Append(':');
                sb.Append(eventId.Name);
            }
            sb.Append(')');
        }
        sb.Append(' ');
        sb.Append(message);
        sb.AppendLine();

        if (ex is not null)
        {
            sb.AppendLine(ex.ToString());
        }

        return sb.ToString();
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
