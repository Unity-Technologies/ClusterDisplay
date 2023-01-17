using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class ObjectsUpdateServiceExtension
    {
        public static void AddObjectsUpdateService(this IServiceCollection services)
        {
            services.AddSingleton<ObjectsUpdateService>();
        }
    }

    /// <summary>
    /// Service listening update from MissionControl's objectUpdate REST entry point.
    /// </summary>
    public class ObjectsUpdateService
    {
        public ObjectsUpdateService(ILogger<ObjectsUpdateService> logger, HttpClient httpClient,
            IConfiguration configuration)
        {
            m_Logger = logger;
            m_HttpClient = httpClient;
            m_HttpClient.BaseAddress = configuration.GetMissionControlUri();
        }

        /// <summary>
        /// Register a callback to be called every time an update for an object is received.
        /// </summary>
        /// <param name="objectName">Name of the object to register for updates.</param>
        /// <param name="onUpdate">Method to be called with the update (when available).</param>
        public void RegisterForUpdates(string objectName, Action<JsonElement> onUpdate)
        {
            if (m_RegisteredObjects.ContainsKey(objectName))
            {
                throw new ArgumentException($"{objectName} is already registered.");
            }

            ObjectInformation objectInformation = new() { OnUpdate = onUpdate };
            m_RegisteredObjects.Add(objectName, objectInformation);
            if (m_RegisteredObjects.Count == 1)
            {
                _ = UpdateLoop();
            }
            else
            {
                m_NewObjectCancellationTokenSource.Cancel();
                m_NewObjectCancellationTokenSource = new();
            }
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

                var firstEntry = m_RegisteredObjects.First();
                uriBuilder.AppendFormat("objectsUpdate?name0={0}&fromVersion0={1}", firstEntry.Key,
                    firstEntry.Value.NextUpdate);
                int entryIndex = 0;
                foreach (var currentEntry in m_RegisteredObjects.Skip(1))
                {
                    ++entryIndex;
                    uriBuilder.AppendFormat("&name{0}={1}&fromVersion{0}={2}", entryIndex, currentEntry.Key,
                        currentEntry.Value.NextUpdate);
                }

                try
                {
                    var updates = await m_HttpClient.GetFromJsonAsync<Dictionary<string,ObservableObjectUpdate>>(
                        uriBuilder.ToString(), Json.SerializerOptions, m_NewObjectCancellationTokenSource.Token);
                    if (updates == null)
                    {
                        continue;
                    }

                    foreach (var registeredObject in m_RegisteredObjects)
                    {
                        if (updates.TryGetValue(registeredObject.Key, out var update))
                        {
                            try
                            {
                                registeredObject.Value.OnUpdate(update.Updated);
                            }
                            catch (Exception e)
                            {
                                m_Logger.LogError(e, "Processing {Object} updates", registeredObject.Key);
                            }
                            registeredObject.Value.NextUpdate = update.NextUpdate;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // A new object was added to the list, let's just restart with the loop to build a new uri.
                }
                catch (Exception e)
                {
                    m_Logger.LogError(e, "Waiting for objects update");
                    await Task.Delay(TimeSpan.FromSeconds(1)); // Wait a little bit before retrying
                }
            }

            // ReSharper disable once FunctionNeverReturns -> This is by design, we always want to update objects until the page is unloaded
        }

        /// <summary>
        /// Information about a registered object.
        /// </summary>
        class ObjectInformation
        {
            /// <summary>
            /// Next update to request
            /// </summary>
            public ulong NextUpdate { get; set; }
            /// <summary>
            /// Callback to call when an update is received for that object.
            /// </summary>
            public Action<JsonElement> OnUpdate { get; init; } = _ => { };
        }

        readonly ILogger m_Logger;
        readonly HttpClient m_HttpClient;

        /// <summary>
        /// To cancel ongoing REST requests.
        /// </summary>
        CancellationTokenSource m_NewObjectCancellationTokenSource = new();
        /// <summary>
        /// Objects registered with the <see cref="RegisterForUpdates"/> method.
        /// </summary>
        Dictionary<string, ObjectInformation> m_RegisteredObjects = new();
    }
}
