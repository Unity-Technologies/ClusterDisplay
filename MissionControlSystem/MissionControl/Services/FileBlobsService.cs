using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class FileBlobsServiceExtension
    {
        public static void AddFileBlobsService(this IServiceCollection services)
        {
            services.AddSingleton<FileBlobsService>();
        }
    }

    /// <summary>
    /// Service managing the list file blobs managed by mission control.
    /// </summary>
    public class FileBlobsService
    {
        public FileBlobsService(ILogger<FileBlobsService> logger,
                                IHostApplicationLifetime applicationLifetime,
                                ConfigService configService,
                                StatusService statusService)
        {
            m_Logger = logger;
            m_ConfigService = configService;

            m_Manager = new(logger);

            applicationLifetime.ApplicationStopping.Register(() =>
            {
                m_Manager.PersistStorageFolderStates();
            });

            // Load current list of storage folders and save any changes done while loading
            using (var lockedStatus = statusService.LockAsync().Result)
            {
                UpdateCacheConfigurationAsync(lockedStatus).Wait();
            }
            m_Manager.PersistStorageFolderStates();

            m_ConfigService.ValidateNew += ValidateNewConfig;
            m_ConfigService.Changed += ConfigChangedAsync;

            _ = PeriodicPersistStorageFoldersState(applicationLifetime.ApplicationStopping);
        }

        /// <summary>
        /// <see cref="FileBlobsManager"/> performing the work of this service.
        /// </summary>
        public FileBlobsManager Manager => m_Manager;

        /// <summary>
        /// Callback responsible for validating a new configuration.
        /// </summary>
        /// <param name="configChange">Information about the configuration change.</param>
        static void ValidateNewConfig(ConfigService.ConfigChangeSurvey configChange)
        {
            if (!configChange.Proposed.StorageFolders.Any())
            {
                configChange.Reject("At least one storage folder is needed.");
                return;
            }

            // Quick check that we can access the folders
            foreach (var folder in configChange.Proposed.StorageFolders)
            {
                string effectivePath = FileBlobsManager.GetEffectiveStoragePath(folder.Path);
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
        /// <param name="lockedStatus">Locked <see cref="StatusService"/> to access status during change notification.
        /// </param>
        Task ConfigChangedAsync(AsyncLockedObject<Status> lockedStatus)
        {
            return UpdateCacheConfigurationAsync(lockedStatus);
        }

        /// <summary>
        /// Update <see cref="m_Manager"/> configurations based on <see cref="ConfigService"/>'s current state.
        /// </summary>
        /// <param name="lockedStatus">Locked <see cref="StatusService"/> to access status during change notification.
        /// </param>
        async Task UpdateCacheConfigurationAsync(AsyncLockedObject<Status> lockedStatus)
        {
            var currentStorageFolders = m_Manager.GetStorageFolderStatus();
            var newConfig = m_ConfigService.Current;

            // First, let's remove "old" folders (that are currently configured in m_Cache and that are not in the new
            // configuration) or update the ones that are still present.  We need to do the remove part first to avoid
            // potential conflicts between files that would be present in folders to be removed and new folders.
            foreach (var oldFolder in currentStorageFolders)
            {
                var newFolderConfig = newConfig.StorageFolders.FirstOrDefault(sf => sf.Path == oldFolder.Path);
                if (newFolderConfig != null)
                {
                    await m_Manager.UpdateStorageFolderAsync(newFolderConfig);
                }
                else
                {
                    await m_Manager.RemoveStorageFolderAsync(oldFolder.Path);
                }
            }

            // Now let's add the new folders
            foreach (var newFolder in newConfig.StorageFolders)
            {
                var newFolderConfig = currentStorageFolders.FirstOrDefault(sf => sf.Path == newFolder.Path);
                if (newFolderConfig == null)
                {
                    m_Manager.AddStorageFolder(newFolder);
                }
            }

            // Update status
            UpdateStatusStorageFolders(lockedStatus);
        }

        /// <summary>
        /// Task being executed periodically in the background to persist the current state of storage folders.
        /// </summary>
        /// <param name="cancellationToken">Indicate that we should stop saving.</param>
        async Task PeriodicPersistStorageFoldersState(CancellationToken cancellationToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                m_Manager.PersistStorageFolderStates();
            }
        }

        /// <summary>
        /// Update <see cref="Status.StorageFolders"/> of <see cref="StatusService"/> if it changed.
        /// </summary>
        /// <param name="lockedStatus">Locked <see cref="StatusService"/> to access status during change notification.
        /// </param>
        void UpdateStatusStorageFolders(AsyncLockedObject<Status> lockedStatus)
        {
            var storageFoldersStatus = m_Manager.GetStorageFolderStatus();

            if (!storageFoldersStatus.SequenceEqual(lockedStatus.Value.StorageFolders))
            {
                lockedStatus.Value.StorageFolders = storageFoldersStatus;
                lockedStatus.Value.SignalChanges();
            }
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;
        readonly ConfigService m_ConfigService;

        /// <summary>
        /// The object doing most of the work
        /// </summary>
        FileBlobsManager m_Manager;
    }
}
