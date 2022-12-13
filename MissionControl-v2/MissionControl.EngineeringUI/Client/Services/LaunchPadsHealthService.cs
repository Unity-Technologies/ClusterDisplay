using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class LaunchPadsHealthExtension
    {
        public static void AddLaunchPadsHealthService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchPadsHealthService>();
        }
    }

    /// <summary>
    /// Service giving access to health of every Launchpad under MissionControl's supervision.
    /// </summary>
    public class LaunchPadsHealthService
    {
        public LaunchPadsHealthService(IncrementalCollectionsUpdateService collectionsUpdateService)
        {
            m_CollectionsUpdateService = collectionsUpdateService;
        }

        /// <summary>
        /// Collection mirroring all the <see cref="LaunchPadStatus"/>es in MissionControl.
        /// </summary>
        public IReadOnlyIncrementalCollection<LaunchPadHealth> Collection => m_Healths;

        /// <summary>
        /// Method to be called by code start wants <see cref="Collection"/> to be regularly updated.
        /// </summary>
        /// <returns><see cref="IDisposable"/> to be disposed of as soon as updates to <see cref="Collection"/> are not
        /// needed anymore.</returns>
        public IDisposable InUse()
        {
            ++m_InUseCount;
            if (m_InUseCount == 1)
            {
                m_CollectionsUpdateService.RegisterForUpdates(k_IncrementalCollectionName, CollectionUpdate);
            }

            return new InUseToken(() => {
                --m_InUseCount;
                if (m_InUseCount == 0)
                {
                    m_CollectionsUpdateService.UnregisterFromUpdates(k_IncrementalCollectionName);
                }
            });
        }

        /// <summary>
        /// <see cref="IDisposable"/> returned by <see cref="InUse"/>.
        /// </summary>
        class InUseToken: IDisposable
        {
            public InUseToken(Action disposedOf)
            {
                m_DisposedOf = disposedOf;
            }

            public void Dispose()
            {
                m_DisposedOf();
                m_DisposedOf = () => { };
            }

            Action m_DisposedOf;
        }

        ulong CollectionUpdate(JsonElement update)
        {
            var deserializeRet = update.Deserialize<IncrementalCollectionUpdate<LaunchPadHealth>>(Json.SerializerOptions);
            if (deserializeRet == null)
            {
                return 0;
            }
            m_Healths.ApplyDelta(deserializeRet);
            return deserializeRet.NextUpdate;
        }

        const string k_IncrementalCollectionName = "launchPadsHealth";
        readonly IncrementalCollectionsUpdateService m_CollectionsUpdateService;

        /// <summary>
        /// The collection mirroring all the <see cref="LaunchPadHealth"/>es in MissionControl.
        /// </summary>
        IncrementalCollection<LaunchPadHealth> m_Healths = new();
        /// <summary>
        /// How many clients are using the service
        /// </summary>
        int m_InUseCount;
    }
}
