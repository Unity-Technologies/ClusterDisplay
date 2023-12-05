using System.Net.Http.Json;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class MissionParametersDesiredValuesServiceExtension
    {
        public static void AddMissionParametersDesiredValuesService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersDesiredValuesService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of <see cref="MissionParameterValue"/> representing the desired values for
    /// <see cref="MissionParameter"/>s in MissionControl and allowing to add new or delete previous desired values.
    /// </summary>
    public class MissionParametersDesiredValuesService
    {
        public MissionParametersDesiredValuesService(HttpClient httpClient, IConfiguration configuration,
            IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();

            collectionsUpdateService.RegisterForUpdates(
                IncrementalCollectionsName.CurrentMissionParametersDesiredValues, CollectionUpdate);
        }

        /// <summary>
        /// Collection mirroring all the desired <see cref="MissionParameterValue"/>s in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<MissionParameterValue> Collection => m_ParametersValues;

        /// <summary>
        /// Create a new or modify an already existing desired <see cref="MissionParameterValue"/> in MissionControl.
        /// </summary>
        /// <param name="toPut">The <see cref="MissionParameterValue"/> to put.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task<HttpResponseMessage> PutAsync(MissionParameterValue toPut)
        {
            return m_HttpClient.PutAsJsonAsync("currentMission/parametersDesiredValues", toPut,
                Json.SerializerOptions);
        }

        /// <summary>
        /// Ask MissionControl to delete the request desired <see cref="MissionParameterValue"/>.
        /// </summary>
        /// <param name="id">Identifier of the <see cref="MissionParameterValue"/> to delete.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task DeleteAsync(Guid id)
        {
            return m_HttpClient.DeleteAsync($"currentMission/parametersDesiredValues/{id}");
        }

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

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// Collection mirroring all the desired <see cref="MissionParameterValue"/>s in MissionControl.
        /// </summary>
        IncrementalCollection<MissionParameterValue> m_ParametersValues = new();
    }
}
