using Microsoft.AspNetCore.Http.Json;

using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.HangarBay.Services;

using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

// Hook for remote management at startup
RemoteManagement remoteManagement = new();
if (!remoteManagement.InterceptStartup())
{
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureAppConfiguration(configure => {
    var argMapping = new Dictionary<string, string>();
    argMapping["-c"] = "configPath";
    argMapping["--configPath"] = "configPath";
    argMapping["-p"] = "payloadsCatalog";
    argMapping["--payloadsCatalog"] = "payloadsCatalog";
    configure.AddCommandLine(Environment.GetCommandLineArgs(), argMapping);
});
builder.WebHost.UseUrls(ConfigService.GetEndpoints(builder.Configuration));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddConfigService();
builder.Services.AddStatusService();
builder.Services.AddFileBlobCacheService();
builder.Services.AddPayloadsService();

builder.Services.Configure<JsonOptions>(options => { Json.AddToSerializerOptions(options.SerializerOptions); });
builder.Services.Configure<MvcJsonOptions>(options => { Json.AddToSerializerOptions(options.JsonSerializerOptions); });
builder.Services.Configure<HostOptions>(options => {
    options.ShutdownTimeout = TimeSpan.FromSeconds(Convert.ToInt32(builder.Configuration["shutdownTimeoutSec"])); });

const string corsPolicyName = "CorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName,
    policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.UseCors(corsPolicyName);

// To ensure that the StatusService is created at startup so that it can get the right startup time.
app.Services.GetService<StatusService>();
// Similar, don't wait for the first request to initialize the content from disk.  Ask the "first service" which will
// trigger creation of the other services (and load everything ready to be used).
app.Services.GetService<PayloadsService>();

// Setup watch of parent process for termination
var appLifetime = app.Services.GetService<IHostApplicationLifetime>();
MasterProcessWatchdog.Setup(appLifetime?.ApplicationStopping ?? CancellationToken.None);

app.Run();

public static class ConfigServiceExtension
{
    /// <summary>
    /// Small helper to be used as a delegate for places where we would get a warning about possible null return values
    /// of <see cref="IServiceProvider.GetService(Type)"/>
    /// </summary>
    /// <typeparam name="T">Type of service</typeparam>
    /// <param name="provider">The service provider</param>
    public static T GetServiceGuaranteed<T>(this IServiceProvider provider)
    {
        return provider.GetService<T>()!;
    }
}
