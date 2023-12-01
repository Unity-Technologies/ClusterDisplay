using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class AssetsServiceExtension
    {
        public static void AddAssetsService(this IServiceCollection services)
        {
            services.AddSingleton<AssetsService>();
        }
    }

    /// <summary>
    /// Service managing the list of assets managed by mission control.
    /// </summary>
    public class AssetsService
    {
        public AssetsService(ILogger<AssetsService> logger,
                             ConfigService configService,
                             PayloadsService payloads,
                             FileBlobsService fileBlobs,
                             IncrementalCollectionCatalogService incrementalCollectionService)
        {
            m_Logger = logger;
            m_Manager = new(m_Logger, payloads.Manager, fileBlobs.Manager);

            m_PersistPath = Path.Combine(configService.PersistPath, "assets.json");
            Load();

            incrementalCollectionService.Register(IncrementalCollectionsName.Assets, RegisterForChangesInCollection,
                GetIncrementalUpdatesAsync);
        }

        /// <summary>
        /// <see cref="AssetsManager"/> performing the work of this service.
        /// </summary>
        public AssetsManager Manager => m_Manager;

        /// <summary>
        /// Save the list of <see cref="Asset"/>s to the file from which <see cref="Load"/> will load it.
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                await using var outputStream = File.OpenWrite(m_PersistPath);
                outputStream.SetLength(0);
                await m_Manager.SaveAsync(outputStream);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to save list of assets to {Path}, will try again in 1 minute", m_PersistPath);
                _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => SaveAsync());
            }
        }

        /// <summary>
        /// Load the list of <see cref="Asset"/>s from the file to which <see cref="SaveAsync"/> saved it.
        /// </summary>
        void Load()
        {
            if (!File.Exists(m_PersistPath))
            {
                m_Logger.LogInformation("Can't find {Path}, starting from an empty list of assets", m_PersistPath);
                return;
            }

            try
            {
                using var fileStream = File.OpenRead(m_PersistPath);
                m_Manager.Load(fileStream);
            }
            catch (Exception e)
            {
                m_Logger.LogCritical(e, "Failed to load list of assets from {Path}, please fix to use MissionControl",
                    m_PersistPath);
                throw;
            }
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to register callback to detect changes in the
        /// collection.
        /// </summary>
        /// <param name="toRegister">The callback to register</param>
        void RegisterForChangesInCollection(Action<IReadOnlyIncrementalCollection> toRegister)
        {
            using var lockedCollection = m_Manager.GetLockedReadOnlyAsync().Result;
            lockedCollection.Value.SomethingChanged += toRegister;
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to get an incremental update from the specified
        /// version.
        /// </summary>
        /// <param name="fromVersion">Version number from which we want to get the incremental update.</param>
        async Task<object?> GetIncrementalUpdatesAsync(ulong fromVersion)
        {
            using var lockedCollection = await m_Manager.GetLockedReadOnlyAsync();
            var ret = lockedCollection.Value.GetDeltaSince(fromVersion);
            return ret.IsEmpty ? null : ret;
        }

        readonly ILogger m_Logger;
        readonly AssetsManager m_Manager;

        /// <summary>
        /// Path that stores the list of <see cref="Asset"/>s.
        /// </summary>
        readonly string m_PersistPath;
    }
}
