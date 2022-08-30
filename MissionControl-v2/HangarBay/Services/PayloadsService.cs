using System.Reflection;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class PayloadsServiceExtension
    {
        public static void AddPayloadsService(this IServiceCollection services)
        {
            services.AddSingleton<PayloadsService>();
        }
    }

    /// <summary>
    /// Service taking care of managing the list of payloads.
    /// </summary>
    public class PayloadsService
    {
        public PayloadsService(IConfiguration configuration, ILogger<PayloadsService> logger,
                               FileBlobCacheService fileBlobCache)
        {
            m_Logger = logger;

            m_Manager = new(logger, configuration["payloadsCatalog"], fileBlobCache.Cache);
        }

        readonly ILogger<PayloadsService> m_Logger;

        /// <summary>
        /// The object doing most of the work
        /// </summary>
        PayloadsManager m_Manager;
    }
}
