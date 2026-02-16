using System.Collections.Concurrent;
using Multispeak.Server.Models;

namespace Multispeak.Server.Services;

/// <summary>
/// Thread-safe in-memory store for virtual meters.
/// </summary>
public interface IMeterStore
{
    IReadOnlyList<Meter> GetAll();
    Meter? GetById(string id);
    Meter? GetByMeterNo(string meterNo);
    Meter? GetByObjectId(string objectId);
    void AddOrUpdate(Meter meter);
    bool Remove(string id);
    /// <summary>Replace all meters (e.g. when restoring from persistence).</summary>
    void LoadAll(IEnumerable<Meter> meters);
}

public class MeterStore : IMeterStore
{
    private readonly ConcurrentDictionary<string, Meter> _byId = new();
    private readonly ConcurrentDictionary<string, string> _meterNoToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _objectIdToId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Meter> GetAll() => _byId.Values.ToList();

    public Meter? GetById(string id) => _byId.TryGetValue(id, out var m) ? m : null;

    public Meter? GetByMeterNo(string meterNo) =>
        _meterNoToId.TryGetValue(meterNo, out var id) ? GetById(id) : null;

    public Meter? GetByObjectId(string objectId) =>
        _objectIdToId.TryGetValue(objectId, out var id) ? GetById(id) : null;

    public void AddOrUpdate(Meter meter)
    {
        var id = meter.MeterNo;
        if (string.IsNullOrWhiteSpace(id))
            return;
        
        var old = _byId.AddOrUpdate(id, meter, (_, _) => meter);

        if (!string.IsNullOrWhiteSpace(meter.MeterNo))
            _meterNoToId[meter.MeterNo] = id;
        if (!string.IsNullOrWhiteSpace(meter.ObjectId))
            _objectIdToId[meter.ObjectId] = id;

        if (old != meter && old.MeterNo != meter.MeterNo && !string.IsNullOrWhiteSpace(old.MeterNo))
            _meterNoToId.TryRemove(old.MeterNo, out _);
        if (old != meter && old.ObjectId != meter.ObjectId && !string.IsNullOrWhiteSpace(old.ObjectId))
            _objectIdToId.TryRemove(old.ObjectId, out _);
    }

    public bool Remove(string id)
    {
        if (!_byId.TryRemove(id, out var meter))
            return false;
        if (!string.IsNullOrWhiteSpace(meter.MeterNo))
            _meterNoToId.TryRemove(meter.MeterNo, out _);
        if (!string.IsNullOrWhiteSpace(meter.ObjectId))
            _objectIdToId.TryRemove(meter.ObjectId, out _);
        return true;
    }

    public void LoadAll(IEnumerable<Meter> meters)
    {
        _byId.Clear();
        _meterNoToId.Clear();
        _objectIdToId.Clear();
        foreach (var m in meters)
            AddOrUpdate(m);
    }
}
