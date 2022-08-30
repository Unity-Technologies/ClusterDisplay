using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Services
{
    public static class ConfigServiceExtension
    {
        public static void AddConfigService(this IServiceCollection services)
        {
            services.AddSingleton<ConfigService>();
            // We need this Lazy access to the service to solve circular dependencies between ConfigService and
            // StatusService.
            services.AddTransient(provider => new Lazy<ConfigService>(provider.GetServiceGuaranteed<ConfigService>));
        }
    }

    /// <summary>
    /// Service managing the main configuration of the service (that can be configured remotely through REST).
    /// </summary>
    public class ConfigService
    {
        public ConfigService(IConfiguration configuration, ILogger<ConfigService> logger,
                             Lazy<StatusService> statusService)
        {
            m_Logger = logger;
            m_StatusService = statusService;

            // We use one setting from the "static" configuration, the location of where to get our own config.json
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_PersistPath = Path.GetFullPath(configuration["configPath"], assemblyFolder!);

            Initialize(configuration);
            m_InitialEndPoints = m_Config.ControlEndPoints.ToArray();

            ValidateNew += ValidateNewConfig;
            Changed += ConfigChanged;
        }

        /// <summary>
        /// Returns the endpoint without having to create the ConfigService.
        /// </summary>
        /// <param name="configuration">.Net configuration coming from various sources (static, does not change
        /// during the execution of the application, often comes from appsettings.json).</param>
        /// <returns>The list of endpoints</returns>
        /// <remarks>Would be nice to simply use <see cref="ConfigService.Current"/>, but for this we need to create
        /// an instance and for this we need dependency injection to be up and running.  However, it is not since we
        /// need to configure the endpoints before starting the service...</remarks>
        public static string[] GetEndpoints(IConfiguration configuration)
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var configPath = Path.GetFullPath(configuration["configPath"], assemblyFolder!);
            try
            {
                if (File.Exists(configPath))
                {
                    using (var loadStream = File.Open(configPath, FileMode.Open))
                    {
                        var config = JsonSerializer.Deserialize<Config>(loadStream);
                        return config.ControlEndPoints.ToArray();
                    }
                }
            }
            catch (Exception)
            {
            }
            return new[] { k_DefaultEndpoint };
        }

        /// <summary>
        /// Current service's configuration.
        /// </summary>
        public Config Current
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Config;
                }
            }
        }

        /// <summary>
        /// Changes the current configuration to the new provided one.
        /// </summary>
        /// <param name="newConfig">New configuration</param>
        /// <returns>List of reasons why it was rejected (or an empty list if changes was done with success).</returns>
        public async Task<IEnumerable<string>> SetCurrent(Config newConfig)
        {
            TaskCompletionSource? setCurrentCompleteTCS = null;
            while (setCurrentCompleteTCS == null)
            {
                Task? toWaitOn = null;
                lock (m_Lock)
                {
                    if (m_ExecutingSetCurrent == null)
                    {
                        setCurrentCompleteTCS = new();
                        m_ExecutingSetCurrent = setCurrentCompleteTCS.Task;
                    }
                    else
                    {
                        toWaitOn = m_ExecutingSetCurrent;
                    }
                }
                if (toWaitOn != null)
                {
                    await toWaitOn;
                }
            }

            try
            {
                // Validate the new configuration
                try
                {
                    var configChange = new ConfigChangeSurvey(newConfig);
                    ValidateNew?.Invoke(configChange);
                    if (!configChange.Accepted)
                    {
                        return configChange.RejectReasons;
                    }
                }
                catch(Exception e)
                {
                    return new[] { e.ToString() };
                }

                // Make the change
                lock (m_Lock)
                {
                    m_Config = newConfig;
                }

                // Broadcast the news
                try
                {
                    var tasks = Changed?.GetInvocationList().Select(d => ((Func<Task>)d)());
                    if (tasks != null && tasks.Any())
                    {
                        await Task.WhenAll(tasks);
                    }
                }
                catch(Exception e)
                {
                    m_Logger.LogError($"Exception informing about an accepted configuration change, sate of some " +
                                      $"services might be out of sync and a restart should be done: {e}");
                    m_StatusService.Value.SignalPendingRestart();
                }

                // And save and return
                Save();

                // Done
                return Enumerable.Empty<string>();
            }
            finally
            {
                lock (m_Lock)
                {
                    Debug.Assert(m_ExecutingSetCurrent == setCurrentCompleteTCS.Task);
                    m_ExecutingSetCurrent = null;
                    setCurrentCompleteTCS.SetResult();
                }
            }
        }

        /// <summary>
        /// Small class providing information about an upcoming configuration change.
        /// </summary>
        public class ConfigChangeSurvey
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="proposed">Newly proposed configuration</param>
            public ConfigChangeSurvey(Config proposed)
            {
                Proposed = proposed;
            }

            /// <summary>
            /// New proposed configuration
            /// </summary>
            public Config Proposed { get; private set; }

            /// <summary>
            /// Is the configuration change accepted by everyone?
            /// </summary>
            public bool Accepted => !m_RejectReasons.Any();

            /// <summary>
            /// Reasons why the new configuration is not acceptable.
            /// </summary>
            public IReadOnlyList<string> RejectReasons => m_RejectReasons;

            /// <summary>
            /// Reject the new current configuration
            /// </summary>
            /// <param name="reason">Reject reasons</param>
            public void Reject(string reason) { m_RejectReasons.Add(reason); }

            /// <summary>
            /// Reasons why the new configuration is not acceptable.
            /// </summary>
            private List<string> m_RejectReasons = new();
        }

        /// <summary>
        /// Event fire to anybody that want to validate a new configuration (and potentially reject it before it gets
        /// applied).
        /// </summary>
        public event Action<ConfigChangeSurvey>? ValidateNew = null;

        /// <summary>
        /// Indicate that the configuration changed.
        /// </summary>
        public event Func<Task>? Changed = null;

        /// <summary>
        /// Initialize the service
        /// </summary>
        /// <param name="configuration">.Net configuration coming from various sources (static, does not change
        /// during the execution of the application, often comes from appsettings.json).</param>
        private void Initialize(IConfiguration configuration)
        {
            bool configLoaded = false;
            try
            {
                if (File.Exists(m_PersistPath))
                {
                    using (var loadStream = File.Open(m_PersistPath, FileMode.Open))
                    {
                        m_Config = JsonSerializer.Deserialize<Config>(loadStream);
                    }
                    m_Logger.LogInformation($"Loaded configuration from {m_PersistPath}.");
                    configLoaded = true;
                }
                else
                {
                    m_Logger.LogInformation("Starting from a brand new default configuration.");
                }
            }
            catch (Exception e)
            {
                m_Logger.LogInformation($"Failed to load {m_PersistPath}, will use the default configuration: {e}");
            }

            if (!configLoaded)
            {
                m_Config.ControlEndPoints = new[] { k_DefaultEndpoint };
                var folderConfig = new StorageFolderConfig();
                folderConfig.Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unity", "Mission Control Cache");
                folderConfig.MaximumSize = new DriveInfo(folderConfig.Path).AvailableFreeSpace / 2;
                m_Config.StorageFolders = new[] { folderConfig };
                Directory.CreateDirectory(Path.GetDirectoryName(m_PersistPath)!);
                Save();
            }
        }

        /// <summary>
        /// Save the current configuration.
        /// </summary>
        private void Save()
        {
            try
            {
                using FileStream serializeStream = File.Create(m_PersistPath);
                JsonSerializer.Serialize(serializeStream, Current);
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Failed to save updated configuration to {m_PersistPath}: {e}");
            }
        }

        /// <summary>
        /// Validate a new configuration
        /// </summary>
        /// <param name="newConfig">Information about the new configuration</param>
        /// <remarks>Normally we want every service to validate their "own part" of the configuration, however some 
        /// parts are not really owned by any actual services (like control endpoints).</remarks>
        private void ValidateNewConfig(ConfigChangeSurvey newConfig)
        {
            foreach (var endpoint in newConfig.Proposed.ControlEndPoints)
            {
                try
                {
                    // Unfortunately Uri does not support parsing uri like : http://*:8100, so we have to check it ourselves.
                    string effectiveEndpoint = endpoint;
                    if (endpoint.ToLower().StartsWith("http://*"))
                    {
                        effectiveEndpoint = "http://0.0.0.0" + endpoint.Substring(8);
                    }

                    var uri = new Uri(effectiveEndpoint);
                    if (uri.Scheme.ToLower() != "http")
                    {
                        newConfig.Reject($"{endpoint} does not start with http://.");
                        return;
                    }
                    if (!IPAddress.TryParse(uri.Host, out var parsedAddress))
                    {
                        newConfig.Reject($"Failed to parse {uri.Host} to an IP address.");
                        return;
                    }
                    if (parsedAddress.Equals(IPAddress.Any) || parsedAddress.Equals(IPAddress.IPv6Any) ||
                        parsedAddress.Equals(IPAddress.Loopback) || parsedAddress.Equals(IPAddress.IPv6Loopback))
                    {
                        continue;
                    }

                    // Try to see if it is one of our local addresses
                    bool found = false;
                    foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (item.OperationalStatus == OperationalStatus.Up)
                        {
                            IPInterfaceProperties adapterProperties = item.GetIPProperties();
                            foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                            {
                                if (ip.Address.Equals(parsedAddress))
                                {
                                    found = true;
                                    break;  // break the loop!!
                                }
                            }
                        }
                        if (found) { break; }
                    }
                    if (!found)
                    {
                        newConfig.Reject($"{uri.Host} does not refer to a local IP address.");
                        return;
                    }
                }
                catch (Exception e)
                {
                    newConfig.Reject($"Error parsing {endpoint}: {e}");
                    return;
                }
            }
        }

        /// <summary>
        /// React to configuration changes
        /// </summary>
        /// <remarks>Normally we want every service to deal with changes in their "own part" of the configuration, 
        /// however some parts are not really owned by any actual services (like control endpoints).</remarks>
        private Task ConfigChanged()
        {
            if (!m_Config.ControlEndPoints.SequenceEqual(m_InitialEndPoints))
            {
                m_StatusService.Value.SignalPendingRestart();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Default endpoint
        /// </summary>
        const string k_DefaultEndpoint = "http://0.0.0.0:8100";

        readonly ILogger<ConfigService> m_Logger;
        readonly Lazy<StatusService> m_StatusService;

        /// <summary>
        /// Where the configuration was from and where to save it.
        /// </summary>
        string m_PersistPath;

        /// <summary>
        /// <see cref="Config.ControlEndPoints"/> at startup.
        /// </summary>
        readonly string[] m_InitialEndPoints;

        /// <summary>
        /// Used to synchronize access to the member variables below.
        /// </summary>
        object m_Lock = new object();

        /// <summary>
        /// Task representing the currently executing <see cref="SetCurrent(Config)"/>, used to serialize concurent
        /// calls to the method.
        /// </summary>
        Task? m_ExecutingSetCurrent;

        /// <summary>
        /// The actual configuration being transmitted and persisted.
        /// </summary>
        Config m_Config;
    }
}
