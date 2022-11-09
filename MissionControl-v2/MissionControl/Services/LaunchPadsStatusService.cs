using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchPad;

using StatusFromLaunchPad = Unity.ClusterDisplay.MissionControl.LaunchPad.Status;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class LaunchPadsStatusServiceExtension
    {
        public static void AddLaunchPadsStatusService(this IServiceCollection services)
        {
            services.AddSingleton<LaunchPadsStatusService>();
        }
    }

    /// <summary>
    /// Service responsible for monitoring status of LaunchdPads.
    /// </summary>
    public class LaunchPadsStatusService: IDisposable
    {
        public LaunchPadsStatusService(ILogger<LaunchPadsStatusService> logger,
                                       ComplexesService complexesService,
                                       IncrementalCollectionCatalogService incrementalCollectionService)
        {
            m_Logger = logger;
            m_ComplexesService = complexesService;

            incrementalCollectionService.Register("launchPadsStatus", m_Collection, GetIncrementalUpdatesAsync);

            // Remarks: I admit, it looks strange to lock in the constructor, but created LaunchStatusUpdaters might
            // start to update the collection as soon as updated.  So let's avoid any chance of problems...
            using (m_Lock.Lock())
            {
                using (var lockedComplexes = m_ComplexesService.Manager.GetLockedReadOnlyAsync().Result)
                {
                    foreach (var complex in lockedComplexes.Value.Values)
                    {
                        foreach (var launchPad in complex.LaunchPads)
                        {
                            LaunchPadStatus newStatus = new LaunchPadStatus(launchPad.Identifier);
                            m_Collection.Add(newStatus);
                            LaunchStatusUpdater statusUpdater = new(this, m_Logger, m_HttpClient, launchPad.Identifier,
                                launchPad.Endpoint);
                            m_Updaters.Add(launchPad.Identifier, statusUpdater);
                        }
                    }

                    lockedComplexes.Value.OnSomethingChanged += LaunchComplexesChanged;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="AsyncLockedObject{T}"/> giving access to a
        /// <see cref="IReadOnlyIncrementalCollection{T}"/> that must be disposed ASAP (as it keeps the
        /// <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        public async Task<AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchPadStatus>>> GetLockedReadOnlyAsync()
        {
            return new AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchPadStatus>>(m_Collection,
                await m_Lock.LockAsync());
        }

        public void Dispose()
        {
            using (m_Lock.Lock())
            {
                foreach (var updater in m_Updaters.Values)
                {
                    updater.Dispose();
                }
                m_Collection.Clear();
                m_Updaters.Clear();
            }
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
        /// Called when something changes in the launch complexes collection.
        /// </summary>
        /// <param name="collection">The collection that changed.</param>
        void LaunchComplexesChanged(IReadOnlyIncrementalCollection collection)
        {
            using (m_Lock.Lock())
            {
                var complexesCollection = (IReadOnlyIncrementalCollection<LaunchComplex>)collection;

                // Go through current launchpads
                HashSet<Guid> launchPadIds = new();
                foreach (var complex in complexesCollection.Values)
                {
                    foreach (var launchPad in complex.LaunchPads)
                    {
                        launchPadIds.Add(launchPad.Identifier);

                        if (!m_Collection.TryGetValue(launchPad.Identifier, out var launchPadStatus))
                        {
                            // New launchpad, let's start monitoring it.
                            launchPadStatus = new LaunchPadStatus(launchPad.Identifier);
                            m_Collection.Add(launchPadStatus);
                            LaunchStatusUpdater statusUpdater = new(this, m_Logger, m_HttpClient,
                                launchPad.Identifier, launchPad.Endpoint);
                            m_Updaters.Add(launchPad.Identifier, statusUpdater);
                        }
                        else
                        {
                            // Already existing launchpad, has it changed so that we need to create a new status
                            // updater?
                            var previousUpdater = m_Updaters[launchPad.Identifier];
                            if (previousUpdater.Endpoint != launchPad.Endpoint)
                            {
                                previousUpdater.Dispose();
                                m_Updaters[launchPad.Identifier] = new(this, m_Logger, m_HttpClient,
                                    launchPad.Identifier, launchPad.Endpoint);
                            }
                        }
                    }
                }

                // Discard any updater for old launchpads
                foreach (var updaterPair in m_Updaters.ToList())
                {
                    if (!launchPadIds.Contains(updaterPair.Key))
                    {
                        bool removed = m_Collection.Remove(updaterPair.Key);
                        Debug.Assert(removed);
                        removed = m_Updaters.Remove(updaterPair.Key);
                        Debug.Assert(removed);
                    }
                }
            }
        }

        /// <summary>
        /// Update a <see cref="LaunchPadStatus"/>.
        /// </summary>
        /// <param name="launchPadId">Launchpad's identifier.</param>
        /// <param name="newStatus">New status from that launchpad.</param>
        void UpdateLaunchPadStatus(Guid launchPadId, StatusFromLaunchPad newStatus)
        {
            using (m_Lock.Lock())
            {
                if (!m_Collection.TryGetValue(launchPadId, out var toUpdate))
                {
                    // Silent fail as this is a normal use case when disposing of a LaunchStatusUpdater.
                    return;
                }

                LaunchPadStatus updatedStatus = new(launchPadId);
                updatedStatus.IsDefined = true;
                updatedStatus.CopyIStatusProperties(newStatus);
                if (updatedStatus.Equals(toUpdate))
                {
                    return;
                }

                toUpdate.DeepCopy(updatedStatus);
                toUpdate.SignalChanges();
            }
        }

        /// <summary>
        /// Update a <see cref="LaunchPadStatus"/> with an update error.
        /// </summary>
        /// <param name="launchPadId">Launchpad's identifier.</param>
        /// <param name="errorDescription">Description of the error updating status.</param>
        void UpdateLaunchPadStatus(Guid launchPadId, string errorDescription)
        {
            using (m_Lock.Lock())
            {
                if (!m_Collection.TryGetValue(launchPadId, out var toUpdate))
                {
                    // Silent fail as this is a normal use case when disposing of a LaunchStatusUpdater.
                    return;
                }

                if (errorDescription == toUpdate.UpdateError)
                {
                    return;
                }

                LaunchPadStatus updatedStatus = new(launchPadId);
                Debug.Assert(updatedStatus.IsDefined == false);
                updatedStatus.UpdateError = errorDescription;

                toUpdate.DeepCopy(updatedStatus);
                toUpdate.SignalChanges();
            }
        }

        /// <summary>
        /// Object responsible for updating status of LaunchPad.
        /// </summary>
        class LaunchStatusUpdater: IDisposable
        {
            public LaunchStatusUpdater(LaunchPadsStatusService owner, ILogger logger, HttpClient httpClient,
                Guid launchPadId, Uri endpoint)
            {
                m_Owner = owner;
                m_Logger = logger;
                m_HttpClient = httpClient;
                m_LaunchPadId = launchPadId;
                m_Endpoint = endpoint;

                Task.Run(UpdateLoopAsync);
            }

            /// <summary>
            /// LaunchPad's endpoint (through which it receives REST calls).
            /// </summary>
            public Uri Endpoint => m_Endpoint;

            public void Dispose()
            {
                m_CancellationTokenSource.Cancel();
            }

            /// <summary>
            /// Continuously running async method that monitor changes in the LaunchPad status.
            /// </summary>
            async Task UpdateLoopAsync()
            {
                ulong minStatusNumber = 0;
                while (!m_CancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Ask for an update
                    StatusFromLaunchPad? launchPadStatus;
                    try
                    {
                        Uri statusUri = new(m_Endpoint, $"api/v1/status?minStatusNumber={minStatusNumber}");
                        var response = await m_HttpClient.GetAsync(statusUri, m_CancellationTokenSource.Token);
                        response.EnsureSuccessStatusCode();
                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            // No content is returned when we have been waiting for "too long" (because there was no
                            // status change).  Simply ask again.
                            continue;
                        }

                        launchPadStatus = JsonSerializer.Deserialize<StatusFromLaunchPad>(
                            await response.Content.ReadAsStreamAsync(m_CancellationTokenSource.Token),
                            Json.SerializerOptions);
                        if (launchPadStatus == null)
                        {
                            throw new NullReferenceException("Unexpected null launchpad status");
                        }
                        minStatusNumber = launchPadStatus.StatusNumber + 1;
                    }
                    catch (Exception e)
                    {
                        // There was a problem getting the launchpad status
                        m_Owner.UpdateLaunchPadStatus(m_LaunchPadId, e.ToString());
                        // Sleep to avoid a loop hammering the system (local and / or remote) for a persistent problem.
                        await Task.Delay(TimeSpan.FromSeconds(30), m_CancellationTokenSource.Token);
                        continue;
                    }

                    // Propagate the update
                    m_Owner.UpdateLaunchPadStatus(m_LaunchPadId, launchPadStatus);
                }
            }

            readonly LaunchPadsStatusService m_Owner;
            // ReSharper disable once NotAccessedField.Local
            readonly ILogger m_Logger;
            readonly HttpClient m_HttpClient;
            readonly Guid m_LaunchPadId;
            readonly Uri m_Endpoint;

            /// <summary>
            /// Used to cancel <see cref="UpdateLoopAsync"/> when disposing of this.
            /// </summary>
            CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();
        }

        readonly ILogger m_Logger;
        readonly HttpClient m_HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable -> Makes sense to keep since we are
        // registered for events from it...
        readonly ComplexesService m_ComplexesService;

        /// <summary>
        /// Used to synchronize access to the member variables below
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// Store the latest status of every LaunchPad
        /// </summary>
        readonly IncrementalCollection<LaunchPadStatus> m_Collection = new();

        /// <summary>
        /// Object responsible for updating the status for each LaunchPad.
        /// </summary>
        Dictionary<Guid, LaunchStatusUpdater> m_Updaters = new();
    }
}
