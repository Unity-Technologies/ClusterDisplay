using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class IncrementalCollectionsUpdateServiceExtension
    {
        public static void AddIncrementalCollectionsUpdateService(this IServiceCollection services)
        {
            services.AddSingleton<IncrementalCollectionsUpdateService>();
        }
    }

    /// <summary>
    /// Service listening update from MissionControl's IncrementalCollection update REST entry point.
    /// </summary>
    public class IncrementalCollectionsUpdateService
    {
        public IncrementalCollectionsUpdateService(ILogger<IncrementalCollectionsUpdateService> logger,
            HttpClient httpClient, IConfiguration configuration)
        {
            m_Logger = logger;
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();
        }

        /// <summary>
        /// Register a callback to be called every time an update for an incremental  collection is received.
        /// </summary>
        /// <param name="collectionName">Name of the incremental collection to register for updates.</param>
        /// <param name="onUpdate">Method to be called with the update (when available).  Receives the unparsed update
        /// and should return the next version number to get.</param>
        public void RegisterForUpdates(string collectionName, Func<JsonElement, ulong> onUpdate)
        {
            if (m_RegisteredCollections.ContainsKey(collectionName))
            {
                throw new ArgumentException($"{collectionName} is already registered.");
            }

            CollectionInformation collectionInformation = new() { OnUpdate = onUpdate };
            m_RegisteredCollections.Add(collectionName, collectionInformation);
            if (!m_UpdateLoopStarted)
            {
                m_UpdateLoopStarted = true;
                _ = UpdateLoop();
            }
            else
            {
                m_NewCollectionCancellationTokenSource.Cancel();
                m_NewCollectionCancellationTokenSource = new();
            }
        }

        /// <summary>
        /// Unregister from updates that have been previously registered by <see cref="RegisterForUpdates"/>.
        /// </summary>
        /// <param name="collectionName">Name of the incremental collection to unregister from updates.</param>
        /// <exception cref="KeyNotFoundException">No matching call to <see cref="RegisterForUpdates"/> found.
        /// </exception>
        public void UnregisterFromUpdates(string collectionName)
        {
            if (!m_RegisteredCollections.Remove(collectionName))
            {
                throw new KeyNotFoundException($"{collectionName} is not already registered.");
            }

            m_NewCollectionCancellationTokenSource.Cancel();
            m_NewCollectionCancellationTokenSource = new();
        }

        /// <summary>
        /// Task constantly running asking for updates.
        /// </summary>
        /// <returns></returns>
        async Task UpdateLoop()
        {
            StringBuilder uriBuilder = new();

            for (; ;)
            {
                uriBuilder.Clear();

                var firstEntry = m_RegisteredCollections.First();
                uriBuilder.AppendFormat("incrementalCollectionsUpdate?name0={0}&fromVersion0={1}", firstEntry.Key,
                    firstEntry.Value.NextUpdate);
                int entryIndex = 0;
                foreach (var currentEntry in m_RegisteredCollections.Skip(1))
                {
                    ++entryIndex;
                    uriBuilder.AppendFormat("&name{0}={1}&fromVersion{0}={2}", entryIndex, currentEntry.Key,
                        currentEntry.Value.NextUpdate);
                }

                try
                {
                    var updates = await m_HttpClient.GetFromJsonAsync<Dictionary<string,JsonElement>>(
                        uriBuilder.ToString(), Json.SerializerOptions, m_NewCollectionCancellationTokenSource.Token);
                    if (updates == null)
                    {
                        continue;
                    }

                    foreach (var registeredCollection in m_RegisteredCollections)
                    {
                        if (updates.TryGetValue(registeredCollection.Key, out var update))
                        {
                            try
                            {
                                var nextUpdate = registeredCollection.Value.OnUpdate(update);
                                if (nextUpdate > 0)
                                {
                                    registeredCollection.Value.NextUpdate = nextUpdate;
                                }
                            }
                            catch (Exception e)
                            {
                                m_Logger.LogError(e, "Processing {Collection} updates", registeredCollection.Key);
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // A new collection was added to the list, let's just restart with the loop to build a new uri.
                }
                catch (Exception e)
                {
                    m_Logger.LogError(e, "Waiting for increment collection update");
                    await Task.Delay(TimeSpan.FromSeconds(1)); // Wait a little bit before retrying
                }
            }

            // ReSharper disable once FunctionNeverReturns -> This is by design, we always want to update collections until the page is unloaded
        }

        /// <summary>
        /// Information about a registered <see cref="IncrementalCollection{T}"/>.
        /// </summary>
        class CollectionInformation
        {
            /// <summary>
            /// Next update to request
            /// </summary>
            public ulong NextUpdate { get; set; }
            /// <summary>
            /// Callback to call when an update is received for that collection.
            /// </summary>
            public Func<JsonElement, ulong> OnUpdate { get; init; } = _ => 0;
        }

        readonly ILogger m_Logger;
        readonly HttpClient m_HttpClient;

        /// <summary>
        /// Is <see cref="UpdateLoop"/> running?
        /// </summary>
        bool m_UpdateLoopStarted;
        /// <summary>
        /// To cancel ongoing REST requests.
        /// </summary>
        CancellationTokenSource m_NewCollectionCancellationTokenSource = new();
        /// <summary>
        /// Collection registered with the <see cref="RegisterForUpdates"/> method.
        /// </summary>
        Dictionary<string, CollectionInformation> m_RegisteredCollections = new();
    }
}
