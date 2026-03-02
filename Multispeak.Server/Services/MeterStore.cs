using MultiSpeak.Server.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace MultiSpeak.Server.Services;

/// <summary>
/// Thread-safe in-memory store for virtual meters.
/// </summary>
public interface IMeterStore
{
    IReadOnlyList<Meter> GetAll();
    Meter? GetById(string id);
    Meter? GetByIcp(string icp);
    Meter? GetByMeterSerialNumber(string serialNo);
    void AddOrUpdate(Meter meter);
    bool Remove(string id);
    /// <summary>Replace all meters (e.g. when restoring from persistence).</summary>
    void LoadAll(IEnumerable<Meter> meters);

    Meter? FindByLocation(string serialNo, string lat, string lon);

    //TODO: tidy this up
    bool Save(IWebHostEnvironment env, JsonSerializerOptions jsonOptions);
}

public class MeterStore : IMeterStore
{
    private readonly ConcurrentDictionary<string, Meter> _byId = new();
    private readonly ConcurrentDictionary<string, string> _icpToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _serialNoToId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Meter> GetAll() => _byId.Values.ToList();

    public Meter? GetById(string id) => _byId.TryGetValue(id, out var m) ? m : null;

    public Meter? GetByIcp(string icp) =>
        _icpToId.TryGetValue(icp, out var id) ? GetById(id) : null;

    public Meter? GetByMeterSerialNumber(string serialNo) =>
        _serialNoToId.TryGetValue(serialNo, out var id) ? GetById(id) : null;

    public void AddOrUpdate(Meter meter)
    {
        var id = meter.Id ?? meter.Icp;
        if (string.IsNullOrWhiteSpace(id))
            return;
        
        var old = _byId.AddOrUpdate(id, meter, (_, _) => meter);

        if (!string.IsNullOrWhiteSpace(meter.Icp))
            _icpToId[meter.Icp] = id;
        if (!string.IsNullOrWhiteSpace(meter.SerialNumber))
            _serialNoToId[meter.SerialNumber] = id;

        if (old != meter && old.Icp != meter.Icp && !string.IsNullOrWhiteSpace(old.Icp))
            _icpToId.TryRemove(old.Icp, out _);
        if (old != meter && old.SerialNumber != meter.SerialNumber && !string.IsNullOrWhiteSpace(old.SerialNumber))
            _serialNoToId.TryRemove(old.SerialNumber, out _);
    }

    public bool Remove(string id)
    {
        if (!_byId.TryRemove(id, out var meter))
            return false;
        if (!string.IsNullOrWhiteSpace(meter.Icp))
            _icpToId.TryRemove(meter.Icp, out _);
        if (!string.IsNullOrWhiteSpace(meter.SerialNumber))
            _serialNoToId.TryRemove(meter.SerialNumber, out _);
        return true;
    }

    public void LoadAll(IEnumerable<Meter> meters)
    {
        _byId.Clear();
        _icpToId.Clear();
        _serialNoToId.Clear();
        foreach (var m in meters)
            AddOrUpdate(m);
    }

    public Meter? FindByLocation(string serialNo, string lat, string lon)
    {
        if (!string.IsNullOrWhiteSpace(serialNo))
        {
            var m = GetByMeterSerialNumber(serialNo);
            if (m != null)
                return m;
        }

        if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetLat))
            return null;
        if (!double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var targetLon))
            return null;

        Meter? best = null;
        var bestDistanceMeters = double.PositiveInfinity;

        foreach (var meter in _byId.Values)
        {
            if (!meter.Lat.HasValue || !meter.Lon.HasValue)
                continue;

            var d = DistanceMeters(targetLat, targetLon, meter.Lat.Value, meter.Lon.Value);
            if (d <= 10.0 && d < bestDistanceMeters)
            {
                best = meter;
                bestDistanceMeters = d;
            }
        }

        return best;
    }

    private static double DistanceMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
    {
        const double R = 6371000.0; // meters

        var lat1 = DegreesToRadians(lat1Deg);
        var lon1 = DegreesToRadians(lon1Deg);
        var lat2 = DegreesToRadians(lat2Deg);
        var lon2 = DegreesToRadians(lon2Deg);

        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;

        var sinDLat = Math.Sin(dLat / 2.0);
        var sinDLon = Math.Sin(dLon / 2.0);

        var a = (sinDLat * sinDLat) +
                (Math.Cos(lat1) * Math.Cos(lat2) * sinDLon * sinDLon);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }

    private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);

    public bool Save(IWebHostEnvironment env, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "meters.json");
            var list = GetAll();
            var json = JsonSerializer.Serialize(list, jsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}

