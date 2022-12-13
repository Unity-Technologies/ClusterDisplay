using Microsoft.AspNetCore.Http.Json;

using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.LaunchPad.Services;

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
    argMapping["-l"] = "launchFolder";
    argMapping["--launchFolder"] = "launchFolder";
    argMapping["-b"] = "blockingCallMaxSec";
    argMapping["--blockingCallMaxSec"] = "blockingCallMaxSec";
    configure.AddCommandLine(Environment.GetCommandLineArgs(), argMapping);
});
builder.WebHost.UseUrls(ConfigService.GetEndpoints(builder.Configuration));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddConfigService();
builder.Services.AddStatusService();
builder.Services.AddCommandProcessor();
builder.Services.AddHealthService();

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
// Create the command processor to be sure it will be present to validate the areas of the configuration is needs to
// validate.
app.Services.GetService<CommandProcessor>();
// Start the health service (since its main goal is to accumulate data before it is asked for)
app.Services.GetService<IHealthService>();

// Setup watch of parent process for termination
var appLifetime = app.Services.GetService<IHostApplicationLifetime>();
MasterProcessWatchdog.Setup(appLifetime?.ApplicationStopping ?? CancellationToken.None);

app.Run();
