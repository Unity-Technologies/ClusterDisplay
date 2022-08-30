using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

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
        public StatusService(ILogger<StatusService> logger, ConfigService configService,
                             FileBlobCacheService fileBlobService)
        {
            m_Logger = logger;
            m_Config = configService;
            m_FileBlobCacheService = fileBlobService;

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (assemblyVersion != null)
            {
                m_HangarBayVersion = assemblyVersion.ToString();
            }
            else
            {
                m_HangarBayVersion = "0.0.0.0";
                m_Logger.LogError($"Failed to get the assembly version, fall-back to {m_HangarBayVersion}.");
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
        /// <returns></returns>
        public Status Build()
        {
            var ret = new Status();
            ret.Version = m_HangarBayVersion;
            ret.StartTime = m_StartTime;
            ret.PendingRestart = m_PendingRestart;
            ret.StorageFolders = m_FileBlobCacheService.Cache.GetStorageFolderStatus();
            return ret;
        }

        readonly ILogger<StatusService> m_Logger;
        readonly ConfigService m_Config;
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
        bool m_PendingRestart = false;
    }
}
