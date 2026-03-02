namespace MultiSpeak.Server.Models;

/// <summary>
/// In-memory virtual meter state for simulation.
/// </summary>
public class Meter
{
    
    public string Icp { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? SerialNumber { get; set; }

    public string ServiceType { get; set; } = "Electric";
    public string? Utility { get; set; }

    public string? Provider { get; set; }
    
    /// <summary>Phase code (e.g. A, AB, ABC).</summary>
    public string? Phases { get; set; }

    public decimal? VoltageReading { get; set; }
    
    public decimal? ActivePowerReading { get; set; }
    
    public decimal? ReactivePowerReading { get; set; }

    public decimal? CurrentReading { get; set; }
    
    public DateTime LastReadingTime { get; set; } = DateTime.UtcNow;

    public bool IsOnline { get; set; } = true;

    public bool CommsStatus { get; set; } = true;

    public double? Lat { get; set; }

    public double? Lon { get; set; }

    public override string ToString()
    {
        return $"ICP:{Icp}, Serial:{SerialNumber}";
    }
}
