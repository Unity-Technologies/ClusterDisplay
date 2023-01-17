using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class LaunchPadsStatusExtension
    {
        public static void AddLaunchPadsStatusService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchPadsStatusService>();
        }
    }

    /// <summary>
    /// Service giving access to status of every Launchpad under MissionControl's supervision.
    /// </summary>
    public class LaunchPadsStatusService
    {
        public LaunchPadsStatusService(IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            collectionsUpdateService.RegisterForUpdates("launchPadsStatus", CollectionUpdate);
        }

        /// <summary>
        /// Collection mirroring all the <see cref="LaunchPadStatus"/>es in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<LaunchPadStatus> Collection => m_Statuses;

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<LaunchPadStatus>>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_Statuses.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        /// <summary>
        /// The collection mirroring all the <see cref="LaunchPadStatus"/>es in MissionControl.
        /// </summary>
        IncrementalCollection<LaunchPadStatus> m_Statuses = new();
    }
}
