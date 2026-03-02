using Microsoft.Extensions.Logging;

namespace MultiSpeak.Server.Logging;

public class FileLoggerOptions
{
    /// <summary>
    /// Directory for log files. Relative paths are resolved from AppContext.BaseDirectory.
    /// </summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>
    /// Minimum log level written to the file.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// File name prefix; files are written as "{prefix}-yyyyMMdd.log".
    /// </summary>
    public string FileNamePrefix { get; set; } = "multispeak";
}
