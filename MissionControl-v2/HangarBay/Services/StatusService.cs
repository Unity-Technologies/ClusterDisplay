using System.Reflection;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class StatusServiceExtension
    {
        public static void AddStatusService(this IServiceCollection services)
        {
            services.AddSingleton<StatusService>();
            // We need this Lazy access to the service to solve circular dependencies between ConfigService and
            // StatusService.
            services.AddTransient(provider => new Lazy<StatusService>(provider.GetServiceGuaranteed<StatusService>));
        }
    }

    /// <summary>
    /// Service storing the current status of the HangarBay.
    /// </summary>
    public class StatusService
    {
        public StatusService(ILogger<StatusService> logger, FileBlobCacheService fileBlobService)
        {
            m_Logger = logger;
            m_FileBlobCacheService = fileBlobService;

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion != null)
            {
                m_HangarBayVersion = assemblyVersion.ToString();
            }
            else
            {
                m_HangarBayVersion = "0.0.0.0";
                m_Logger.LogError("Failed to get the assembly version, fall-back to {HangarBayVersion}",
                    m_HangarBayVersion);
            }
        }

        /// <summary>
        /// Does the HangarBay has to be restarted?
        /// </summary>
        public bool HasPendingRestart => m_PendingRestart;

        /// <summary>
        /// Indicate that the HangarBay that requires a restart.
        /// </summary>
        public void SignalPendingRestart()
        {
            m_PendingRestart = true;
        }

        /// <summary>
        /// Build a <see cref="Status"/> from the current state.
        /// </summary>
        /// <returns>The built <see cref="Status"/>.</returns>
        public Status Build()
        {
            var ret = new Status()
            {
                Version = m_HangarBayVersion,
                StartTime = m_StartTime,
                PendingRestart = m_PendingRestart,
                StorageFolders = m_FileBlobCacheService.GetStorageFolderStatus()
            };
            return ret;
        }

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        readonly ILogger<StatusService> m_Logger;
        readonly FileBlobCacheService m_FileBlobCacheService;

        /// <summary>
        /// Version of the HangarBay
        /// </summary>
        readonly string m_HangarBayVersion;

        /// <summary>
        /// Startup time of the HangarBay.
        /// </summary>
        readonly DateTime m_StartTime = DateTime.Now;

        /// <summary>
        /// Has some operations been done on the HangarBay that requires a restart?
        /// </summary>
        bool m_PendingRestart;
    }
}
