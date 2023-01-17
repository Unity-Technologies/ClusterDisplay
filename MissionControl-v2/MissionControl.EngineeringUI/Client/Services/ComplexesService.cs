using System.Net.Http.Json;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class ComplexesServiceExtension
    {
        public static void AddComplexesService(this IServiceCollection services)
        {
            services.AddSingleton<ComplexesService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of <see cref="LaunchComplex"/> in MissionControl and allowing to update that
    /// list.
    /// </summary>
    public class ComplexesService
    {
        public ComplexesService(HttpClient httpClient, IConfiguration configuration,
            IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();

            collectionsUpdateService.RegisterForUpdates("complexes", CollectionUpdate);
        }

        /// <summary>
        /// Collection mirroring all the <see cref="LaunchComplex"/>es in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<LaunchComplex> Collection => m_Complexes;

        /// <summary>
        /// Put (create a new or update an already existing) <see cref="LaunchComplex"/> in MissionControl.
        /// </summary>
        /// <param name="toPut">To be put in MissionControl.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task PutAsync(LaunchComplex toPut)
        {
            return m_HttpClient.PutAsJsonAsync("complexes", toPut, Json.SerializerOptions);
        }

        /// <summary>
        /// Ask MissionControl to delete the request <see cref="LaunchComplex"/>.
        /// </summary>
        /// <param name="id">Identifier of the <see cref="LaunchComplex"/> to delete.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task DeleteAsync(Guid id)
        {
            return m_HttpClient.DeleteAsync($"complexes/{id}");
        }

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<LaunchComplex>>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_Complexes.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// The collection mirroring all the <see cref="LaunchComplex"/>es in MissionControl.
        /// </summary>
        IncrementalCollection<LaunchComplex> m_Complexes = new();
    }
}
