using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Unity.ClusterDisplay.MissionControl.EngineeringUI;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddHttpClient();

builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<TooltipService>();

builder.Services.AddUiGlobalService();
builder.Services.AddObjectsUpdateService();
builder.Services.AddIncrementalCollectionsUpdateService();
builder.Services.AddMissionControlStatusService();
builder.Services.AddLaunchConfigurationService();
builder.Services.AddComplexesService();
builder.Services.AddAssetsService();
builder.Services.AddMissionsService();
builder.Services.AddMissionCommandsService();
builder.Services.AddLaunchPadsStatusService();
builder.Services.AddLaunchPadsHealthService();

await builder.Build().RunAsync();
