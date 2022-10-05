using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Services
{
    public static class HealthServiceExtension
    {
        public static void AddHealthService(this IServiceCollection services)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                services.AddSingleton<IHealthService, HealthServiceWindows>();
            }
            else
            {
                services.AddSingleton<IHealthService, DummyHealthService>();
            }
        }
    }

    /// <summary>
    /// Interface for the service responsible to fetch the current system's health (implementation is OS dependent).
    /// </summary>
    public interface IHealthService
    {
        /// <summary>
        /// Fetch the current health report of the system.
        /// </summary>
        Health Fetch();
    }

    /// <summary>
    /// Placeholder <see cref="IHealthService"/> implementation to avoid problems when running on platforms for which
    /// we do not have a <see cref="IHealthService"/> implementation yet.
    /// </summary>
    class DummyHealthService: IHealthService
    {
        public DummyHealthService(ILogger<HealthServiceWindows> logger)
        {
            m_Logger = logger;
        }

        public Health Fetch()
        {
            if (!m_GenerateErrorOnce)
            {
                m_Logger.LogError("Missing IHealthService implementation for current platform");
                m_GenerateErrorOnce = true;
            }
            return new Health();
        }

        readonly ILogger m_Logger;
        bool m_GenerateErrorOnce = false;
    }
}
