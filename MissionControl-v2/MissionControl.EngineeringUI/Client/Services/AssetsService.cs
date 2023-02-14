using System.Net.Http.Json;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class AssetsServiceExtension
    {
        public static void AddAssetsService(this IServiceCollection services)
        {
            services.AddSingleton<AssetsService>();
        }
    }

    /// <summary>
    /// Service giving access to the list of <see cref="Asset"/> in MissionControl and allowing to add new or delete
    /// assets.
    /// </summary>
    public class AssetsService
    {
        public AssetsService(HttpClient httpClient, IConfiguration configuration,
            IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();

            collectionsUpdateService.RegisterForUpdates(IncrementalCollectionsName.Assets, CollectionUpdate);
        }

        /// <summary>
        /// The collection mirroring all the <see cref="Asset"/>s in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<Asset> Collection => m_Assets;

        /// <summary>
        /// Create a new <see cref="Asset"/> in MissionControl.
        /// </summary>
        /// <param name="toPost">Describes the new <see cref="Asset"/> to add to MissionControl.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public async Task<Guid> PostAsync(AssetPost toPost)
        {
            var responseMessage = await m_HttpClient.PostAsJsonAsync("assets", toPost, Json.SerializerOptions);
            if (responseMessage.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<Guid>(await responseMessage.Content.ReadAsStringAsync());
            }
            else
            {
                throw new InvalidOperationException(await responseMessage.Content.ReadAsStringAsync());
            }
        }

        /// <summary>
        /// Ask MissionControl to delete the request <see cref="Asset"/>.
        /// </summary>
        /// <param name="id">Identifier of the <see cref="Asset"/> to delete.</param>
        /// <remarks>We do not update <see cref="Collection"/> immediately, we wait for the update from MissionControl
        /// the same way as we would receive updates if it was done by some other device.</remarks>
        public Task DeleteAsync(Guid id)
        {
            return m_HttpClient.DeleteAsync($"assets/{id}");
        }

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<Asset>>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_Assets.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        readonly HttpClient m_HttpClient;

        /// <summary>
        /// The collection mirroring all the <see cref="Asset"/>s in MissionControl.
        /// </summary>
        IncrementalCollection<Asset> m_Assets = new();
    }
}
