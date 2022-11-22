using Unity.ClusterDisplay.MissionControl.MissionControl.Library;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class PayloadsServiceExtension
    {
        public static void AddPayloadsService(this IServiceCollection services)
        {
            services.AddSingleton<PayloadsService>();
        }
    }

    /// <summary>
    /// Service managing the list payloads managed by mission control.
    /// </summary>
    public class PayloadsService
    {
        public PayloadsService(ILogger<PayloadsService> logger,
                               ConfigService configService,
                               FileBlobsService fileBlobs)
        {
            m_Logger = logger;
            m_Manager = new(m_Logger, Path.Combine(configService.PersistPath, "payloads"), fileBlobs.Manager);
        }

        /// <summary>
        /// <see cref="PayloadsManager"/> performing the work of this service.
        /// </summary>
        public PayloadsManager Manager => m_Manager;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        readonly ILogger m_Logger;
        readonly PayloadsManager m_Manager;
    }
}
