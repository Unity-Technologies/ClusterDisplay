using Unity.ClusterDisplay.MissionControl.LaunchPad;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class LaunchPadsHealthServiceExtension
    {
        public static void AddLaunchPadsHealthService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchPadsHealthService>();
        }
    }

    /// <summary>
    /// Service responsible for monitoring health of LaunchdPads.
    /// </summary>
    public class LaunchPadsHealthService: IDisposable
    {
        public LaunchPadsHealthService(ILogger<LaunchPadsHealthService> logger,
                                       ConfigService configService,
                                       ComplexesService complexesService,
                                       IncrementalCollectionCatalogService incrementalCollectionService)
        {
            m_Logger = logger;
            m_ConfigService = configService;
            m_ComplexesService = complexesService;

            m_ConfigService.ValidateNew += ValidateNewConfig;
            m_ConfigService.Changed += ConfigChangedAsync;
            m_MonitoringInterval = TimeSpan.FromSeconds(m_ConfigService.Current.HealthMonitoringIntervalSec);

            incrementalCollectionService.Register("launchPadsHealth", m_Collection, GetIncrementalUpdatesAsync);

            Task.Run(UpdateLoopAsync);
        }

        /// <summary>
        /// Returns a <see cref="AsyncLockedObject{T}"/> giving access to a
        /// <see cref="IReadOnlyIncrementalCollection{T}"/> that must be disposed ASAP (as it keeps the
        /// <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        public async Task<AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchPadHealth>>> GetLockedReadOnlyAsync()
        {
            return new AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchPadHealth>>(m_Collection,
                await m_Lock.LockAsync());
        }

        public void Dispose()
        {
            m_CancellationTokenSource.Cancel();
            m_ConfigService.ValidateNew -= ValidateNewConfig;
            m_ConfigService.Changed -= ConfigChangedAsync;
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to get an incremental update from the specified
        /// version.
        /// </summary>
        /// <param name="fromVersion">Version number from which we want to get the incremental update.</param>
        async Task<object?> GetIncrementalUpdatesAsync(ulong fromVersion)
        {
            using (await m_Lock.LockAsync())
            {
                var ret = m_Collection.GetDeltaSince(fromVersion);
                return ret.IsEmpty ? null : ret;
            }
        }

        /// <summary>
        /// Loop that takes care of updating LaunchPads health.
        /// </summary>
        async Task UpdateLoopAsync()
        {
            while (!m_CancellationTokenSource.IsCancellationRequested)
            {
                Task nextUpdateWaitTask;
                Task monitoringIntervalChangedTask;
                using (await m_Lock.LockAsync())
                {
                    nextUpdateWaitTask = Task.Delay(m_MonitoringInterval, m_CancellationTokenSource.Token);
                    monitoringIntervalChangedTask = m_MonitoringIntervalChanged.Task;
                }

                CancellationTokenSource thisUpdateCancelSource = new();

                // Initiate getting health from every known launchpad
                Dictionary<Guid, Task<LaunchPadHealth>> healthFetch = new();
                using (var lockedComplexes = await m_ComplexesService.Manager.GetLockedReadOnlyAsync())
                {
                    foreach (var launchComplex in lockedComplexes.Value.Values)
                    {
                        foreach (var launchPad in launchComplex.LaunchPads)
                        {
                            Uri healthUri = new(launchPad.Endpoint, "api/v1/health");
                            var fetchTask = m_HttpClient.GetFromJsonAsync<Health>(healthUri, Json.SerializerOptions,
                                    thisUpdateCancelSource.Token);
                            var complementTask = fetchTask.ContinueWith(t => {
                                LaunchPadHealth healthWithTime = new(launchPad.Identifier);
                                healthWithTime.UpdateTime = DateTime.Now;
                                if (t.Result == null)
                                {
                                    throw new NullReferenceException("Unexpected null Health");
                                }
                                healthWithTime.IsDefined = true;
                                healthWithTime.DeepCopyFrom(t.Result);
                                return healthWithTime;
                            }, thisUpdateCancelSource.Token);
                            healthFetch.Add(launchPad.Identifier,complementTask);
                        }
                    }
                }

                // Update the collection to deal with new / old launchpads
                using (await m_Lock.LockAsync())
                {
                    // Add new entries to the collection
                    foreach (var fetchedLaunchPadId in healthFetch.Keys)
                    {
                        if (!m_Collection.ContainsKey(fetchedLaunchPadId))
                        {
                            m_Collection.Add(new (fetchedLaunchPadId));
                        }
                    }

                    // Remove old entries from the collection
                    foreach (var collectionLaunchPadId in m_Collection.Keys.ToList())
                    {
                        if (!healthFetch.ContainsKey(collectionLaunchPadId))
                        {
                            m_Collection.Remove(collectionLaunchPadId);
                        }
                    }
                }

                // Wait for up to 1 second to get all responses
                var firstStepTask = Task.Delay(TimeSpan.FromSeconds(1), m_CancellationTokenSource.Token);
                await Task.WhenAny(nextUpdateWaitTask, firstStepTask, monitoringIntervalChangedTask,
                    Task.WhenAll(healthFetch.Values));

                // Let's update results we got
                UpdateCollectionWithCompletedTask(healthFetch);

                // Do we still have some uncompleted updates
                if (healthFetch.Any())
                {
                    // Yes, wait for them to complete
                    await Task.WhenAny(nextUpdateWaitTask, monitoringIntervalChangedTask,
                        Task.WhenAll(healthFetch.Values));

                    // And again update the collection
                    UpdateCollectionWithCompletedTask(healthFetch);
                }

                // Do we still have some update pending, if yes generate a warning and move on to the next update
                if (healthFetch.Any())
                {
                    m_Logger.LogWarning("Failed to get health update from launchpads {LaunchPadsList}, expect some " +
                        "health information to be outdated",
                        String.Join(", ", healthFetch.Keys.Select(id => id.ToString())));
                    thisUpdateCancelSource.Cancel();
                }

                // Wait for next refresh
                await Task.WhenAny(monitoringIntervalChangedTask, nextUpdateWaitTask);
            }
        }

        /// <summary>
        /// Update the <see cref="LaunchPadHealth"/> collection with the new health from completed tasks.
        /// </summary>
        /// <param name="stillWaitingFor"><see cref="Dictionary{TKey, TValue}"/> where the key is the
        /// LaunchPad identifier and the value is the task providing it.  The collection will be updated to only keep
        /// uncompleted tasks.</param>
        void UpdateCollectionWithCompletedTask(Dictionary<Guid, Task<LaunchPadHealth>> stillWaitingFor)
        {
            foreach (var fetchPair in stillWaitingFor.ToList())
            {
                if (fetchPair.Value.IsCompleted)
                {
                    try
                    {
                        LaunchPadHealth update = fetchPair.Value.Result;
                        // No need to spend time comparing to skip update when the same, UpdateTime will always
                        // be different.
                        m_Collection[fetchPair.Key] = update;
                    }
                    catch (Exception e)
                    {
                        LaunchPadHealth inErrorUpdate = new(fetchPair.Key);
                        inErrorUpdate.UpdateError = e.ToString();

                        if (!inErrorUpdate.Equals(m_Collection[fetchPair.Key]))
                        {
                            m_Collection[fetchPair.Key] = inErrorUpdate;
                        }
                    }
                    stillWaitingFor.Remove(fetchPair.Key);
                }
            }
        }

        /// <summary>
        /// Validate a new configuration
        /// </summary>
        /// <param name="newConfig">Information about the new configuration</param>
        /// <remarks>Normally we want every service to validate their "own part" of the configuration, however some
        /// parts are not really owned by any actual services (like control endpoints).</remarks>
        static void ValidateNewConfig(ConfigService.ConfigChangeSurvey newConfig)
        {
            if (newConfig.Proposed.HealthMonitoringIntervalSec <= 0.0)
            {
                newConfig.Reject("HealthMonitoringIntervalSec must be > 0");
            }
        }

        /// <summary>
        /// React to configuration changes
        /// </summary>
        async Task ConfigChangedAsync(AsyncLockedObject<Status> _)
        {
            TaskCompletionSource? toSignal = null;
            using (await m_Lock.LockAsync())
            {
                var newInterval = TimeSpan.FromSeconds(m_ConfigService.Current.HealthMonitoringIntervalSec);
                if (newInterval != m_MonitoringInterval)
                {
                    m_MonitoringInterval = newInterval;
                    toSignal = m_MonitoringIntervalChanged;
                    m_MonitoringIntervalChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            toSignal?.TrySetResult();
        }

        readonly ILogger m_Logger;
        readonly HttpClient m_HttpClient = new();
        readonly ConfigService m_ConfigService;
        readonly ComplexesService m_ComplexesService;

        /// <summary>
        /// Used to cancel <see cref="UpdateLoopAsync"/> when disposing of this.
        /// </summary>
        CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Used to synchronize access to the member variables below
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// Store the latest status of every LaunchPad
        /// </summary>
        readonly IncrementalCollection<LaunchPadHealth> m_Collection = new();

        /// <summary>
        /// Health monitoring interval (from <see cref="m_ConfigService"/>).
        /// </summary>
        TimeSpan m_MonitoringInterval;

        /// <summary>
        /// Task that completes when the monitoring interval changes.
        /// </summary>
        TaskCompletionSource m_MonitoringIntervalChanged = new();
    }
}
