using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionParametersEffectiveValuesServiceExtension
    {
        public static void AddMissionParametersEffectiveValuesService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersEffectiveValuesService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of <see cref="MissionParameterValue"/> representing the effective values for
    /// <see cref="MissionParameter"/>s in MissionControl.
    /// </summary>
    public class MissionParametersEffectiveValuesService
    {
        public MissionParametersEffectiveValuesService(IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            collectionsUpdateService.RegisterForUpdates(
                IncrementalCollectionsName.CurrentMissionParametersEffectiveValues, CollectionUpdate);
        }

        /// <summary>
        /// Collection mirroring all the effective <see cref="MissionParameterValue"/>s in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<MissionParameterValue> Collection => m_ParametersValues;

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<MissionParameterValue>>(
                Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_ParametersValues.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        /// <summary>
        /// Collection mirroring all the effective <see cref="MissionParameterValue"/>s in MissionControl.
        /// </summary>
        IncrementalCollection<MissionParameterValue> m_ParametersValues = new();
    }
}
