namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class MissionParametersEffectiveValuesServiceExtension
    {
        public static void AddMissionParametersEffectiveValuesService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersEffectiveValuesService>();
        }
    }

    /// <summary>
    /// Service manging the list of effective <see cref="MissionParameterValue"/>.
    /// </summary>
    public class MissionParametersEffectiveValuesService:
        MissionParametersValuesIdentifierCollectionServiceBase<MissionParameterValue>
    {
        public MissionParametersEffectiveValuesService(
            IncrementalCollectionCatalogService incrementalCollectionCatalogService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService)
            : base(currentMissionLaunchConfigurationService)
        {
            Register(incrementalCollectionCatalogService,
                IncrementalCollectionsName.CurrentMissionParametersEffectiveValues);
        }
    }
}
