namespace Multispeak.Server.Models;

/// <summary>
/// In-memory virtual meter state for simulation.
/// </summary>
public class Meter
{
    public string MeterNo { get; set; } = string.Empty;
    public string? ObjectId { get; set; }
    public string ServiceType { get; set; } = "Electric";
    public string? Utility { get; set; }
    
    /// <summary>Phase code (e.g. A, AB, ABC).</summary>
    public string? Phases { get; set; }

    public decimal? VoltageReading { get; set; }
    
    public decimal? ActivePowerReading { get; set; }
    
    public decimal? ReactivePowerReading { get; set; }

    public decimal? CurrentReading { get; set; }
    
    public DateTime LastReadingTime { get; set; } = DateTime.UtcNow;

    public bool IsOnline { get; set; } = true;

    public bool CommsStatus { get; set; } = true;

}
