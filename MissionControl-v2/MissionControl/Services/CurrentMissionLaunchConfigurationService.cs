using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class LaunchConfigurationServiceExtension
    {
        public static void AddLaunchConfigurationService(this IServiceCollection services)
        {
            services.AddSingleton<CurrentMissionLaunchConfigurationService>();
        }
    }

    /// <summary>
    /// Service to interact with the current mission's <see cref="LaunchConfiguration"/>.
    /// </summary>
    /// <remarks>We could be tempted to validate everything in this service (that the <see cref="LaunchConfiguration"/>
    /// references existing <see cref="LaunchComplex"/>es or <see cref="LaunchPad"/>s).  But we don't, instead the
    /// mapping gets resolved when preparing the launch skipping missing elements.  This is especially useful for
    /// loading a saved mission referencing <see cref="LaunchComplex"/>es or <see cref="LaunchPad"/>s that have been
    /// deleted and are added back before the launch.</remarks>
    public class CurrentMissionLaunchConfigurationService: ObservableObjectService<LaunchConfiguration>
    {
        public CurrentMissionLaunchConfigurationService(ILogger<CurrentMissionLaunchConfigurationService> logger,
            IServiceProvider serviceProvider,
            ConfigService configService)
            : base(serviceProvider, new LaunchConfiguration(), "currentMission/launchConfiguration")
        {
            m_Logger = logger;

            m_PersistPath = Path.Combine(configService.PersistPath, "currentMission/launchConfiguration.json");
            Directory.CreateDirectory(Path.GetDirectoryName(m_PersistPath)!);
            Load();
        }

        /// <summary>
        /// Save the current mission's configuration to the file from which <see cref="Load"/> will load it.
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                using var launchConfiguration = await LockAsync();
                await using var outputStream = File.OpenWrite(m_PersistPath);
                outputStream.SetLength(0);
                await JsonSerializer.SerializeAsync(outputStream, launchConfiguration.Value,
                    Json.SerializerOptions);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to save current launch configuration to {Path}, will try again in " +
                    "1 minute", m_PersistPath);
                _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => SaveAsync());
            }
        }

        /// <summary>
        /// Load the current mission's configuration from the file to which <see cref="SaveAsync"/> saved it.
        /// </summary>
        /// <remarks>Should only be called from the constructor.</remarks>
        void Load()
        {
            if (!File.Exists(m_PersistPath))
            {
                m_Logger.LogInformation("Can't find {Path}, starting from an empty current launch configuration",
                    m_PersistPath);
                return;
            }

            try
            {
                LaunchConfiguration? launchConfiguration;
                using (var fileStream = File.OpenRead(m_PersistPath))
                {
                    launchConfiguration = JsonSerializer.Deserialize<LaunchConfiguration>(fileStream,
                        Json.SerializerOptions);
                    if (launchConfiguration == null)
                    {
                        throw new NullReferenceException($"Got a null LaunchConfiguration deserializing " +
                            $"{m_PersistPath}.");
                    }
                }

                using (var lockedLaunchConfiguration = LockAsync().Result)
                {
                    lockedLaunchConfiguration.Value.DeepCopy(launchConfiguration);
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to load current launch configuration from {Path}, starting from an empty " +
                    "current launch configuration", m_PersistPath);
                throw;
            }
        }

        readonly ILogger m_Logger;

        /// <summary>
        /// Path that stores the list of <see cref="Asset"/>s.
        /// </summary>
        readonly string m_PersistPath;
    }
}
