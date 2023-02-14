using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class MissionParametersDesiredValuesServiceExtension
    {
        public static void AddMissionParametersDesiredValuesService(this IServiceCollection services)
        {
            services.AddSingleton<MissionParametersDesiredValuesService>();
        }
    }

    /// <summary>
    /// Service manging the list of desired <see cref="MissionParameterValue"/>.
    /// </summary>
    public class MissionParametersDesiredValuesService:
        MissionParametersValuesIdentifierCollectionServiceBase<MissionParameterValue>
    {
        public MissionParametersDesiredValuesService(
            ILogger<MissionParametersDesiredValuesService> logger,
            IHostApplicationLifetime applicationLifetime,
            ConfigService configService,
            IncrementalCollectionCatalogService incrementalCollectionCatalogService,
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService)
            : base(currentMissionLaunchConfigurationService)
        {
            m_Logger = logger;

            m_PersistPath = Path.Combine(configService.PersistPath, "currentMission",
                "desiredMissionParametersValues.json");
            Directory.CreateDirectory(Path.GetDirectoryName(m_PersistPath)!);
            Load();

            Register(incrementalCollectionCatalogService,
                IncrementalCollectionsName.CurrentMissionParametersDesiredValues);

            _ = SaveLoop(applicationLifetime.ApplicationStopping);
        }

        /// <summary>
        /// Load the current mission's desired parameters values from the file to which <see cref="SaveLoop"/> saved
        /// it.
        /// </summary>
        /// <remarks>Should only be called from the constructor.</remarks>
        void Load()
        {
            if (!File.Exists(m_PersistPath))
            {
                m_Logger.LogInformation("Can't find {Path}, starting from a default set of mission parameters",
                    m_PersistPath);
                return;
            }

            try
            {
                MissionParameterValue[]? launchParametersValues;
                using (var fileStream = File.OpenRead(m_PersistPath))
                {
                    launchParametersValues = JsonSerializer.Deserialize<MissionParameterValue[]>(fileStream,
                        Json.SerializerOptions);
                    if (launchParametersValues == null)
                    {
                        throw new NullReferenceException($"Got a null array of MissionParameterValue deserializing " +
                            $"{m_PersistPath}.");
                    }
                }

                SetAll(launchParametersValues);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to load current desired value for mission parameters from {Path}, " +
                    "starting from a default set of mission parameters", m_PersistPath);
                throw;
            }
        }

        /// <summary>
        /// Function that periodically look for changes in the list of values and save them if anything changes.
        /// </summary>
        /// <param name="cancellationToken">Signaled when the application wants to stop.</param>
        /// <returns>We for now go with the simple approach of saving everything every k_SaveInterval when something
        /// changes.  This might not be the most fastest approach but it is fairly simple and considering the number
        /// of parameter values should be fairly low (&lt;100) this shouldn't cause any negative side effects.</returns>
        async Task SaveLoop(CancellationToken cancellationToken)
        {
            ulong lastSavedVersion = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait a little bit before saving
                await Task.Delay(k_SaveInterval, cancellationToken).ConfigureAwait(false);

                // Ask for changes
                ulong previousVersionSaved = lastSavedVersion;
                var allValues = CloneAllIfChanged(ref lastSavedVersion);
                if (allValues != null)
                {
                    try
                    {
                        await using var outputStream = File.OpenWrite(m_PersistPath);
                        outputStream.SetLength(0);
                        await JsonSerializer.SerializeAsync(outputStream, allValues, Json.SerializerOptions,
                            cancellationToken);
                    }
                    catch (Exception e)
                    {
                        m_Logger.LogError(e, "Failed to save current desired mission parameters values to {Path}, " +
                            "will try again in {Interval} seconds", m_PersistPath, k_SaveInterval.TotalSeconds);
                        lastSavedVersion = previousVersionSaved;
                    }
                }
            }
        }

        readonly ILogger m_Logger;

        /// <summary>
        /// Path that stores the list of <see cref="Asset"/>s.
        /// </summary>
        readonly string m_PersistPath;

        static readonly TimeSpan k_SaveInterval = TimeSpan.FromSeconds(2);
    }
}
