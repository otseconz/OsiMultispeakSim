using MultiSpeak.Server.Contracts;
using MultiSpeak.Server.Models;
using MultiSpeak.Server.Services;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace MultiSpeak.Server.Api;

/// <summary>
/// Maps and implements the REST API endpoints for meters and MultiSpeak notifications.
/// </summary>
public static class MeterEndpoints
{
    private const int MaxTake = 500;
    private const string MultiSpeakServerApiMeterendpoints = "MultiSpeak.Server.Api.MeterEndpoints";

    public static void MapMeterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/meters").WithTags("Meters");

        group.MapGet("", GetAll);

        group.MapGet("{id}", GetById);

        group.MapPost("", AddOrUpdate);

        group.MapDelete("{id}", Delete);

        group.MapPost("persist", Persist);

        group.MapPost("import-premises", ImportPremises).DisableAntiforgery();

        group.MapPost("{id}/od-event", SendOdEvent);

        group.MapPost("{id}/meter-event", SendMeterEvent);

        group.MapPost("{id}/reading-changed", SendReadingChanged);
    }

    /// <summary>
    /// Retrieves Meters with optional filtering.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="filter">The filter text to search for, fields searched are the MeterNo, ObjectId, ServiceType, Utility</param>
    /// <param name="skip">How many results to skip(for pagination)</param>
    /// <param name="take">How many results to return (max 500)</param>
    /// <returns></returns>
    private static IResult GetAll(
        IMeterStore store,
        string? filter,
        int? skip,
        int? take)
    {
        var all = store.GetAll();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim();
            all = all.Where(m =>
                (m.Icp?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Id?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.SerialNumber?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.ServiceType?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Utility?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        if (take is > 0)
        {
            var total = all.Count;
            var pageSize = Math.Min(take.Value, MaxTake);
            var items = all.Skip(skip ?? 0).Take(pageSize).ToList();
            return Results.Json(new { items, total });
        }

        return Results.Json(all);
    }

    /// <summary>
    /// Get a meter by Id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="store"></param>
    /// <returns></returns>
    private static IResult GetById(
        [Description("MeterNo")] string id,
        IMeterStore store)
    {
        var m = store.GetById(id);
        return m is null ? Results.NotFound() : Results.Json(m);
    }

    /// <summary>
    /// Add or update a meter
    /// </summary>
    /// <param name="meter"></param>
    /// <param name="store"></param>
    /// <param name="loggerFactory"></param>
    /// <returns></returns>
    private static IResult AddOrUpdate(Meter meter, IMeterStore store, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(MultiSpeakServerApiMeterendpoints);
        logger.LogInformation($"Add/Update for meter {meter}");

        if (string.IsNullOrWhiteSpace(meter.Icp))
            return Results.BadRequest("MeterNo required");
        store.AddOrUpdate(meter);
        return Results.Json(meter);
    }

    /// <summary>
    /// Delete a meter
    /// </summary>
    /// <param name="id"></param>
    /// <param name="store"></param>
    /// <param name="loggerFactory"></param>
    /// <returns></returns>
    private static IResult Delete(string id, IMeterStore store, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(MultiSpeakServerApiMeterendpoints);
        logger.LogInformation($"Deleting meter with id {id}");

        return store.Remove(id) ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>
    /// Persist the meter store to disk (meters.json)
    /// </summary>
    /// <param name="store"></param>
    /// <param name="env"></param>
    /// <param name="jsonOptions"></param>
    /// <param name="loggerFactory"></param>
    /// <returns></returns>
    private static IResult Persist(IMeterStore store, IWebHostEnvironment env, JsonSerializerOptions jsonOptions, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(MultiSpeakServerApiMeterendpoints);
        logger.LogInformation($"Saving the meter store");

        return store.Save(env, jsonOptions) ? Results.Ok(new { success = true }) : Results.StatusCode(500);
    }

    /// <summary>
    /// Import meters from premises XML
    /// </summary>
    /// <param name="store"></param>
    /// <param name="file"></param>
    /// <returns></returns>
    private static async Task<IResult> ImportPremises(IMeterStore store, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Results.Json(new { success = false, error = "No file uploaded." }, statusCode: 400);
        var contentType = file.ContentType ?? "";
        if (!contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) && !file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { success = false, error = "File must be XML." }, statusCode: 400);
        try
        {
            await using var stream = file.OpenReadStream();
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var root = doc.Root;
            if (root == null)
                return Results.Json(new { success = false, error = "Empty XML." }, statusCode: 400);
            var added = 0;
            foreach (var premise in root.Elements().Where(e => e.Name.LocalName == "premise"))
            {
                var services = premise.Elements().FirstOrDefault(e => e.Name.LocalName == "services");
                double? lat = double.TryParse(premise.Attribute("geoY")?.Value, out var l1) ? l1 : null;
                double? lon = double.TryParse(premise.Attribute("geoX")?.Value, out var l2) ? l2 : null;
                if (services == null) continue;
                foreach (var service in services.Elements().Where(e => e.Name.LocalName == "service"))
                {
                    var meterIdAttr = service.Attribute("meterID");
                    var meterId = meterIdAttr?.Value?.Trim();
                    if (string.IsNullOrEmpty(meterId)) continue;
                    var typeAttr = service.Attribute("type");
                    var serviceType = typeAttr?.Value?.Trim() ?? "Electric";
                    var phasesAttr = service.Attribute("phases");
                    var phaseCode = PhasesToPhaseCode(phasesAttr?.Value);
                    var meter = new Meter
                    {
                        Icp = meterId,
                        Id = meterId,
                        ServiceType = serviceType,
                        Phases = phaseCode,
                        VoltageReading = 0,
                        ActivePowerReading = 0,
                        ReactivePowerReading = 0,
                        CurrentReading = 0,
                        LastReadingTime = DateTime.UtcNow,
                        IsOnline = true,
                        Lat = lat,
                        Lon = lon
                    };
                    store.AddOrUpdate(meter);
                    added++;
                }
            }
            return Results.Json(new { success = true, added });
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Send a ODEventNotification message to the OMS system
    /// </summary>
    /// <param name="id"></param>
    /// <param name="server"></param>
    /// <returns></returns>
    private static async Task<IResult> SendOdEvent(string id, MultiSpeakService server)
    {
        try
        {
         
            server.InitiateOutageDetectionEventRequest(id);
            //TODO: we should return more info here
            return await Task.FromResult(Results.Json(new { success = true, errors = new List<ErrorObject>() }));
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Send a MeterEventNotification message to the OMS system
    /// </summary>
    /// <param name="id"></param>
    /// <param name="multiSpeakService"></param>
    /// <returns></returns>
    private static async Task<IResult> SendMeterEvent(string id, MultiSpeakService multiSpeakService)
    {
        try
        {
            multiSpeakService.InitiateMeterEventRequest(id);
            //TODO: we should return more info here
            return await Task.FromResult(Results.Json(new { success = true, errors = new List<ErrorObject>() }));
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>
    /// Send a ReadingChangedNotification message to the OMS system
    /// </summary>
    /// <param name="id"></param>
    /// <param name="multiSpeakService"></param>
    /// <returns></returns>
    private static async Task<IResult> SendReadingChanged(string id, MultiSpeakService multiSpeakService)
    {
        try
        {
            multiSpeakService.InitiateMeterReadingsByMeterID(id);
            //TODO: we should return more info here
            return await Task.FromResult(Results.Json(new { success = true, errors = new List<ErrorObject>() }));
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
        }
    }

    /// <summary>Convert OMS phase codes 123 to ABC. 1→A, 2→B, 3→C.</summary>
    private static string? PhasesToPhaseCode(string? phases)
    {
        if (string.IsNullOrWhiteSpace(phases)) return null;
        var sb = new StringBuilder();
        foreach (var c in phases.Trim())
        {
            var letter = c switch { '1' => 'A', '2' => 'B', '3' => 'C', _ => (char?)null };
            if (letter.HasValue && sb.ToString().IndexOf(letter.Value) < 0)
                sb.Append(letter.Value);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
