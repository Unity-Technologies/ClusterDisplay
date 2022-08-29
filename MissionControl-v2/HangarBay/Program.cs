using Unity.ClusterDisplay.MissionControl.HangarBay.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ConfigService.GetEndpoints(builder.Configuration));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddConfigService();
builder.Services.AddStatusService();
builder.Services.AddFileBlobCacheService();
builder.Services.AddPayloadsService();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

// To ensure that the StatusService is created at startup so that it can get the right startup time.
app.Services.GetService<StatusService>();
// Similar, don't wait for the first request to initialize the content from disk.  Ask the "first service" which will
// trigger creation of the other services (and load everything ready to be used).
app.Services.GetService<PayloadsService>();

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
