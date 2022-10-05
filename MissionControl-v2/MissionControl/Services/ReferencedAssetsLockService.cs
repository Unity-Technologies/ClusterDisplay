namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class ReferencedAssetsLockServiceExtension
    {
        public static void AddReferencedAssetsLockService(this IServiceCollection services)
        {
            services.AddSingleton<ReferencedAssetsLockService>();
        }
    }

    /// <summary>
    /// Service used to lock the list of referenced assets.
    /// </summary>
    /// <remarks>This is to be used by any part of the code that need to ensure that no other threads modify the list
    /// at the same time.  This service should always be locked first before any other lock to be sure to avoid any
    /// potential deadlocks.</remarks>
    public class ReferencedAssetsLockService
    {
        /// <summary>
        /// Call to lock modification of the list of referenced <see cref="Asset"/>s and ensure no <see cref="Asset"/>s
        /// are deleted.
        /// </summary>
        public Task<IDisposable> LockAsync()
        {
            return m_Lock.LockAsync();
        }

        AsyncLock m_Lock = new();
    }
}
