using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MultiSpeak.Server;
using MultiSpeak.Server.Api;
using MultiSpeak.Server.Configuration;
using MultiSpeak.Server.Contracts;
using MultiSpeak.Server.Logging;
using MultiSpeak.Server.Models;
using MultiSpeak.Server.Services;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Text.Json;

using EnvelopeVersion = CoreWCF.EnvelopeVersion;

var plugins = new List<IMultiSpeakPlugin>();

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

var builder = WebApplication.CreateBuilder(args);
// Add Windows Service support
builder.Host.UseWindowsService();

builder.Services.AddSingleton(jsonOptions);
builder.Configuration.AddUserSecrets<Program>();

builder.Logging.AddFile(o =>
{
    o.LogDirectory = "logs";
    o.FileNamePrefix = "multispeak";
    o.MinimumLevel = LogLevel.Debug;
});

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
        Title = "MultiSpeak Server API",
        Description = "OSI OMS MultiSpeak simulator",
    });
});

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();

//TODO: Load plugins here
var pluginSection = builder.Configuration.GetSection("Plugins");
var pluginAssemblyPaths = pluginSection.Get<string[]>() ?? Array.Empty<string>();

var pluginsFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
var pluginLog = Path.Combine(pluginsFolder, "pluginErrors.txt");
foreach (var pluginDll in pluginAssemblyPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
{
    try
    {
        var path = Path.Combine(pluginsFolder, pluginDll!.Trim());

        if (!File.Exists(path))
        {

            Console.WriteLine($"Plugin assembly not found at '{path}', skipping.");
            File.AppendAllText(pluginLog, $"Plugin assembly not found at '{path}', skipping.");
            continue;
        }

        var assembly = Assembly.LoadFrom(path);
        var pluginTypes = assembly
            .GetTypes()
            .Where(t => typeof(IMultiSpeakPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IMultiSpeakPlugin plugin)
            {
                plugin.Setup(builder);
                plugins.Add(plugin);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load plugin assembly '{pluginDll}': {ex.Message}");
        File.AppendAllText(pluginLog, $"Failed to load plugin assembly '{pluginDll}': {ex.Message}");
    }
}

var app = builder.Build();
var logger = app.Services.GetService<ILogger<Program>>();

var metersPath = Path.Combine(app.Environment.ContentRootPath, "meters.json");

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MultiSpeak Server API V1");
    //options.RoutePrefix = string.Empty; // Swagger at root URL
});

app.MapMeterEndpoints();

// Add SOAP endpoints
app.UseServiceModel(builder =>
{
    builder.AddService<MultiSpeakService>(serviceOptions =>
    {
        serviceOptions.DebugBehavior.IncludeExceptionDetailInFaults = true;
    });
    // SOAP 1.1 (text/xml) - default endpoint
    builder.AddServiceEndpoint<MultiSpeakService, IOMS_MultiSpeak_v41_Soap>(new CoreWCF.BasicHttpBinding(), "/MultiSpeak");
    
    // SOAP 1.2 (application/soap+xml) - requests rewritten here by middleware when Content-Type is application/soap+xml
    var soap12Binding = new CustomBinding(
        new TextMessageEncodingBindingElement(
            MessageVersion.CreateVersion(
                EnvelopeVersion.Soap12,
                AddressingVersion.None),
            Encoding.UTF8),
        new HttpTransportBindingElement()
    );

    builder.AddServiceEndpoint<MultiSpeakService, IOMS_MultiSpeak_v41_Soap>(soap12Binding, "/MultiSpeak12");
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
            logger?.LogWarning(ex, "Could not restore meters from {Path}", metersPath);
        }
    }
}

//TODO: start plugin tasks here
foreach (var plugin in plugins)
{
    try
    {
        logger?.LogInformation("Running BeforeRun for plugin {PluginType}", plugin.GetType().FullName);
        plugin.BeforeRun(app);
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "Error running BeforeRun for plugin {PluginType}", plugin.GetType().FullName);
    }
}

app.Run();
