namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class CapcomUplinkServiceExtension
    {
        public static void AddCapcomUplinkService(this IServiceCollection services)
        {
            services.AddSingleton<CapcomUplinkService>();
        }
    }

    public class CapcomUplinkService: ObservableObjectService<CapcomUplink>
    {
        public CapcomUplinkService(ILogger<CapcomUplinkService> logger,
            IHostApplicationLifetime applicationLifetime,
            ObservableObjectCatalogService catalogService)
            : base(applicationLifetime, catalogService, new CapcomUplink(), "capcomUplink")
        {
        }
    }
}
