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
                throw new NotImplementedException("So far this is only implemented for Windows");
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
}
