using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionParametersServiceExtension
    {
        public static void AddMissionParametersService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of <see cref="MissionParameter"/> in MissionControl.
    /// </summary>
    public class MissionParametersService
    {
        public MissionParametersService(IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            collectionsUpdateService.RegisterForUpdates(IncrementalCollectionsName.CurrentMissionParameters,
                CollectionUpdate);
        }

        /// <summary>
        /// The collection mirroring all the <see cref="MissionParameter"/>s in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<MissionParameter> Collection => m_MissionParameters;

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<MissionParameter>>(
                Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_MissionParameters.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        /// <summary>
        /// The collection mirroring all the <see cref="MissionParameter"/>s in MissionControl.
        /// </summary>
        IncrementalCollection<MissionParameter> m_MissionParameters = new();
    }
}
