using Microsoft.AspNetCore.Http.Json;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.MissionControl.MissionControl.Services;

using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureAppConfiguration(configure => {
    var argMapping = new Dictionary<string, string>();
    argMapping["-c"] = "configPath";
    argMapping["--configPath"] = "configPath";
    argMapping["-a"] = "capcomFolder";
    argMapping["--capcomFolder"] = "capcomFolder";
    argMapping["-t"] = "testService";
    argMapping["--testService"] = "testService";
    configure.AddCommandLine(Environment.GetCommandLineArgs(), argMapping);
});
builder.WebHost.UseUrls(ConfigService.GetEndpoints(builder.Configuration));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCommandsService();
builder.Services.AddObservableObjectCatalogService();
builder.Services.AddIncrementalCollectionCatalogService();
builder.Services.AddConfigService();
builder.Services.AddStatusService();
builder.Services.AddFileBlobsService();
builder.Services.AddPayloadsService();
builder.Services.AddReferencedAssetsLockService();
builder.Services.AddAssetsService();
builder.Services.AddComplexesService();
builder.Services.AddLaunchPadsStatusService();
builder.Services.AddLaunchPadsHealthService();
builder.Services.AddLaunchConfigurationService();
builder.Services.AddMissionsService();
builder.Services.AddMissionCommandsService();
builder.Services.AddLaunchService();
builder.Services.AddCapcomUplinkService();

// Add the service to work with the automated tests
bool hasTestService = !string.IsNullOrEmpty(builder.Configuration["testService"]) &&
    Convert.ToBoolean(builder.Configuration["testService"]);
if (hasTestService)
{
    builder.Services.AddTestService();
}

builder.Services.Configure<JsonOptions>(options => { Json.AddToSerializerOptions(options.SerializerOptions); });
builder.Services.Configure<MvcJsonOptions>(options => { Json.AddToSerializerOptions(options.JsonSerializerOptions); });
//builder.Services.Configure<HostOptions>(options => {
//    options.ShutdownTimeout = TimeSpan.FromSeconds(Convert.ToInt32(builder.Configuration["shutdownTimeoutSec"])); });

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
// Similar, don't wait for the first request to initialize the content from disk.  Ask the "top level services" which
// will trigger creation of the other services (and load everything ready to be used).
app.Services.GetService<AssetsService>();
app.Services.GetService<ComplexesService>();
app.Services.GetService<LaunchPadsStatusService>();
app.Services.GetService<LaunchPadsHealthService>();
app.Services.GetService<CurrentMissionLaunchConfigurationService>();
app.Services.GetService<MissionsService>();
app.Services.GetService<LaunchService>();
if (hasTestService)
{
    app.Services.GetService<TestService>();
}

// Setup watch of parent process for termination
var appLifetime = app.Services.GetService<IHostApplicationLifetime>();
MasterProcessWatchdog.Setup(appLifetime?.ApplicationStopping ?? CancellationToken.None);

app.Run();
