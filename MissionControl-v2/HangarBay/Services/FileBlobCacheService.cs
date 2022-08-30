using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class FileBlobCacheServiceExtension
    {
        public static void AddFileBlobCacheService(this IServiceCollection services)
        {
            services.AddSingleton<FileBlobCacheService>();
        }
    }

    /// <summary>
    /// Service responsible to manage a cache of file blobs (from which we copy files to the LaunchPad folders).
    /// </summary>
    public class FileBlobCacheService
    {
        public FileBlobCacheService(ILogger<FileBlobCacheService> logger, ConfigService configService)
        {
            m_Logger = logger;
            m_ConfigService = configService;
            m_Cache = new(logger);

            // Load current list of storage folders and save any changes done while loading
            UpdateCacheConfiguration().Wait();
            m_Cache.PersistStorageFolderStates();

            m_ConfigService.ValidateNew += ValidateNewConfig;
            m_ConfigService.Changed += Configchanged;
        }

        /// <summary>
        /// The object managing the cache
        /// </summary>
        public FileBlobCache Cache => m_Cache;

        /// <summary>
        /// Callback responsible for validating a new configuration.
        /// </summary>
        /// <param name="configChange">Information about the configuration change.</param>
        private void ValidateNewConfig(ConfigService.ConfigChangeSurvey configChange)
        {
            if (!configChange.Proposed.StorageFolders.Any())
            {
                configChange.Reject("At least one storage folder is needed.");
                return;
            }

            // Quick check that we can access the folders
            foreach (var folder in configChange.Proposed.StorageFolders)
            {
                string effectivePath = FileBlobCache.GetEffectiveStoragePath(folder.Path);
                if (!Directory.Exists(effectivePath))
                {
                    try
                    {
                        Directory.CreateDirectory(effectivePath);
                    }
                    catch (Exception)
                    {
                        configChange.Reject($"Can't access or create {effectivePath}.");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Callback responsible to update our configuration when the configuration changes.
        /// </summary>
        private Task Configchanged()
        {
            return UpdateCacheConfiguration();
        }

        /// <summary>
        /// Update <see cref="m_Cache"/> configurations based on <see cref="ConfigService"/>'s current state.
        /// </summary>
        private async Task UpdateCacheConfiguration()
        {
            var currentStorageFolders = m_Cache.GetStorageFolderStatus();
            var newConfig = m_ConfigService.Current;

            // First, let's remove "old" folders (that are currently configured in m_Cache and that are not in the new
            // configuration) or update the ones that are still present.  We need to do the remove part first to avoid
            // potential conflicts between files that would be present in folders to be removed and new folders.
            foreach (var oldFolder in currentStorageFolders)
            {
                var newFolderConfig = newConfig.StorageFolders.Where(sf => sf.Path == oldFolder.Path).FirstOrDefault();
                if (newFolderConfig != null)
                {
                    m_Cache.UpdateStorageFolder(newFolderConfig);
                }
                else
                {
                    await m_Cache.RemoveStorageFolderAsync(oldFolder.Path);
                }
            }

            // Now let's add the new folders
            foreach (var newFolder in newConfig.StorageFolders)
            {
                var newFolderConfig = currentStorageFolders.Where(sf => sf.Path == newFolder.Path).FirstOrDefault();
                if (newFolderConfig == null)
                {
                    m_Cache.AddStorageFolder(newFolder);
                }
            }
        }

        readonly ILogger<FileBlobCacheService> m_Logger;
        readonly ConfigService m_ConfigService;

        /// <summary>
        /// The object doing most of the work
        /// </summary>
        FileBlobCache m_Cache;
    }
}
