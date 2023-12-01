namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class ConfigurationExtensions
    {
        public static Uri GetMissionControlUri(this IConfiguration configuration)
        {
            return new Uri(new Uri(configuration["MissionControlUri"]), "api/v1/");
        }
    }
}
