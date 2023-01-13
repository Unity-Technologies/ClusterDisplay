using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionsServiceExtension
    {
        public static void AddMissionsService(this IServiceCollection services)
        {
            services.AddSingleton<MissionsService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of saved <see cref="SavedMissionSummary"/> in MissionControl and allows to
    /// delete previously saved missions.
    /// </summary>
    public class MissionsService
    {
        public MissionsService(HttpClient httpClient, IConfiguration configuration,
            IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();

            collectionsUpdateService.RegisterForUpdates(IncrementalCollectionsName.Missions, CollectionUpdate);
        }

        /// <summary>
        /// Collection mirroring all the <see cref="LaunchComplex"/>es in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<SavedMissionSummary> Collection => m_Missions;

        /// <summary>
        /// Ask MissionControl to delete the request <see cref="SavedMissionSummary"/>.
        /// </summary>
        /// <param name="id">Identifier of the <see cref="SavedMissionSummary"/> to delete.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task DeleteAsync(Guid id)
        {
            return m_HttpClient.DeleteAsync($"missions/{id}");
        }

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet =
                update.Deserialize<IncrementalCollectionUpdate<SavedMissionSummary>>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_Missions.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// The collection mirroring all the <see cref="SavedMissionSummary"/>s in MissionControl.
        /// </summary>
        IncrementalCollection<SavedMissionSummary> m_Missions = new();
    }
}
