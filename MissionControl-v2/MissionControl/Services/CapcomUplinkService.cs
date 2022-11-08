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
        public CapcomUplinkService(IServiceProvider serviceProvider, ILogger<CapcomUplinkService> logger)
            : base(serviceProvider, new CapcomUplink(), "capcomUplink")
        {
        }
    }
}
