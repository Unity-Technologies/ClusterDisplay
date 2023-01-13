namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class MissionParametersServiceExtension
    {
        public static void AddMissionParametersService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersService>();
        }
    }

    /// <summary>
    /// Service manging the list of effective <see cref="MissionParameter"/>.
    /// </summary>
    public class MissionParametersService: MissionParametersValuesIdentifierCollectionServiceBase<MissionParameter>
    {
        public MissionParametersService(
            IncrementalCollectionCatalogService incrementalCollectionCatalogService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService)
            : base(currentMissionLaunchConfigurationService)
        {
            Register(incrementalCollectionCatalogService, IncrementalCollectionsName.CurrentMissionParameters);
        }
    }
}
