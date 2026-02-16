using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Multispeak.Server.Configuration;
using Multispeak.Server.Contracts;
using Multispeak.Server.Models;
using Multispeak.Server.Services;
using System.ComponentModel;
using System.ServiceModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EnvelopeVersion = CoreWCF.EnvelopeVersion;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddHttpContextAccessor(); //can probably get rid of this soon

builder.Services.Configure<MultiSpeakClientOptions>(builder.Configuration.GetSection(MultiSpeakClientOptions.SectionName));
builder.Services.AddSingleton<IMeterStore, MeterStore>();
builder.Services.AddHttpClient();

//this is the client that we will use to send data back to OMS
builder.Services.AddSingleton<ChannelFactory<IOMS_MultiSpeak_v41_Soap>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MultiSpeakClientOptions>>().Value;

    var binding = new BasicHttpBinding(System.ServiceModel.BasicHttpSecurityMode.None);
    var endpoint = new EndpointAddress($"{options.BaseUrl}{options.MultiSpeakPath}");

    return new ChannelFactory<IOMS_MultiSpeak_v41_Soap>(binding, endpoint);
});
builder.Services.AddSingleton<MultiSpeakClient>();

//this is the service that will receive calls from OMS
builder.Services.AddSingleton<MultiSpeakService>();
builder.Services.AddSingleton<IOMS_MultiSpeak_v41_Soap>(sp => sp.GetRequiredService<MultiSpeakService>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Path to the generated XML file  
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    options.IncludeXmlComments(xmlPath);
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Multispeak Server API",
        Description = "OSI OMS Multispeak simulator",
    });
});

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();

var app = builder.Build();
var metersPath = Path.Combine(app.Environment.ContentRootPath, "meters.json");

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Multispeak Server API V1");
    //options.RoutePrefix = string.Empty; // Swagger at root URL
});


app.MapGet("/api/meters", (IMeterStore store,
        [Description("The filter text to search for, fields searched are the MeterNo, ObjectId, ServiceType, Utility")]
        string? filter,
        [Description("How many results to skip(for pagination")]
        int? skip,
        [Description("How many results to return (max 500")]
        int? take) =>
    {
        const int maxTake = 500;
        var all = store.GetAll();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter!.Trim();
            all = all.Where(m =>
                //(m.Id?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.MeterNo?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.ObjectId?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.ServiceType?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Utility?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        if (take is > 0)
        {
            var total = all.Count;
            var pageSize = Math.Min(take.Value, maxTake);
            var items = all.Skip(skip ?? 0).Take(pageSize).ToList();
            return Results.Json(new { items, total });
        }

        return Results.Json(all);
    })
    .WithSummary("Retrieves Meters with optional filtering.");

app.MapGet("/api/meters/{id}", (
    [Description("MeterNo")]
    string id, 
    IMeterStore store) =>
{
    var m = store.GetByMeterNo(id);
    return m is null ? Results.NotFound() : Results.Json(m);
}).WithSummary("Get a meter by MeterNo");

app.MapPost("/api/meters", (Meter meter, IMeterStore store) =>
{
    if (string.IsNullOrWhiteSpace(meter.MeterNo))
        return Results.BadRequest("MeterNo required");
    store.AddOrUpdate(meter);
    return Results.Json(meter);
}).WithSummary("Add or update a meter");

app.MapDelete("/api/meters/{id}", (
        [Description("The MeterNo")]
        string id, IMeterStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound()).WithSummary("Delete a meter");

app.MapPost("/api/meters/persist", (IMeterStore store, IWebHostEnvironment env) =>
{
    try
    {
        var path = Path.Combine(env.ContentRootPath, "meters.json");
        var list = store.GetAll();

        var json = JsonSerializer.Serialize(list, jsonOptions);
        File.WriteAllText(path, json);
        return Results.Json(new { success = true, count = list.Count });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
}).WithSummary("Persist the meter store to disk (meters.json)");

// Import meters from premises XML (one meter per <service> under <premise><services>)
app.MapPost("/api/meters/import-premises", async (IMeterStore store, IFormFile file, HttpRequest req) =>
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
                var deviceAttr = service.Attribute("device");
                var meter = new Meter
                {
                    MeterNo = meterId,
                    ObjectId = meterId,
                    ServiceType = serviceType,
                    Phases = phaseCode,
                    VoltageReading = 0,
                    ActivePowerReading = 0,
                    ReactivePowerReading = 0,
                    CurrentReading = 0,
                    LastReadingTime = DateTime.UtcNow,
                    IsOnline = true
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
}).DisableAntiforgery().WithSummary("Import meters from premises XML");



// Send ODEventNotification for a specific meter to the configured client
app.MapPost("/api/meters/{id}/od-event", async (
    [Description("The MeterNo")]
    string id, MultiSpeakClient client, MultiSpeakService server, IMeterStore store) =>
{
    try
    {
        var response = await server.SendOdEventNotificationAsync([id]);
        
        return Results.Json(new { success = true, errors = response.ODEventNotificationResult?.errorObject ?? [] });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
}).WithSummary("Send a ODEventNotification message to the OMS system"); ; ;

// Send ODEventNotification for a specific meter to the configured client
app.MapPost("/api/meters/{id}/meter-event", async (
    [Description("The MeterNo")]
    string id, MultiSpeakClient client, MultiSpeakService multiSpeakService, IMeterStore store) =>
{
    try
    {
        var response = await multiSpeakService.SendMeterEventNotificationAsync(id);
        
        return Results.Json(new { success = true, errors = response?.MeterEventNotificationResult?.errorObject ?? [] });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
}).WithSummary("Send a MeterEventNotification message to the OMS system"); ;

app.MapPost("/api/meters/{id}/reading-changed", async (
    [Description("The MeterNo")]
    string id, MultiSpeakClient client, MultiSpeakService multiSpeakService) =>
{
    try
    {
        var response = multiSpeakService.SendReadingChangedAsync([id]);
        return Results.Json(new { success = true, errors = response.Result?.ReadingChangedNotificationResult?.errorObject ?? [] });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
}).WithSummary("Send a ReadingChangedNotification message to the OMS system");

// Add SOAP endpoints
app.UseServiceModel(builder =>
{
    builder.AddService<MultiSpeakService>(serviceOptions =>
    {
        serviceOptions.DebugBehavior.IncludeExceptionDetailInFaults = true;
    });
    // SOAP 1.1 (text/xml) - default endpoint
    builder.AddServiceEndpoint<MultiSpeakService, IOMS_MultiSpeak_v41_Soap>(new CoreWCF.BasicHttpBinding(), "/Multispeak");
    
    // SOAP 1.2 (application/soap+xml) - requests rewritten here by middleware when Content-Type is application/soap+xml
    var soap12Binding = new CustomBinding(
        new TextMessageEncodingBindingElement(
            MessageVersion.CreateVersion(
                EnvelopeVersion.Soap12,
                AddressingVersion.None),
            Encoding.UTF8),
        new HttpTransportBindingElement()
    );

    builder.AddServiceEndpoint<MultiSpeakService, IOMS_MultiSpeak_v41_Soap>(soap12Binding, "/Multispeak12");
});


// Restore meters from persistence at startup
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IMeterStore>();
    if (File.Exists(metersPath))
    {
        try
        {
            var json = await File.ReadAllTextAsync(metersPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var restored = JsonSerializer.Deserialize<List<Meter>>(json, options);
            if (restored is { Count: > 0 })
                store.LoadAll(restored);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Could not restore meters from {Path}", metersPath);
        }
    }
}

app.Run();


// Convert OMS phase codes 123 to ABC
// 1→A, 2→B, 3→C; "1"→"A", "12"→"AB", "123"→"ABC"
static string? PhasesToPhaseCode(string? phases)
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
