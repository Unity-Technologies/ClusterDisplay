namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class TestServiceExtension
    {
        public static void AddTestService(this IServiceCollection services)
        {
            services.AddSingleton<TestService>();
        }
    }

    /// <summary>
    /// Small service to work in tandem with MissionControl.Tests to perform some automated tests.
    /// </summary>
    /// <remarks>Only created when the right command line parameters are added to the command line.</remarks>
    public class TestService
    {
        public TestService(ILogger<TestService> logger,
                           ConfigService configService,
                           AssetsService assetsService,
                           CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService)
        {
            m_Logger = logger;
            m_ConfigService = configService;
            m_AssetsService = assetsService;
            m_CurrentMissionLaunchConfigurationService = currentMissionLaunchConfigurationService;

            m_ConfigService.Changed += ConfigChangedAsync;
            using (var lockedAssets = m_AssetsService.Manager.GetLockedReadOnlyAsync().Result)
            {
                lockedAssets.Value.OnObjectRemoved += AssetRemoved;
            }
            using (var lockedLaunchConfiguration = m_CurrentMissionLaunchConfigurationService.LockAsync().Result)
            {
                lockedLaunchConfiguration.Value.OnObjectChanged += LaunchConfigurationChanged;
            }
        }

        /// <summary>
        /// Callback responsible to update our configuration when the configuration changes.
        /// </summary>
        /// <remarks>In the case of the test service we "stall" application of the new configuration if the storage
        /// folder contains a directory named "stall" until it contains a file named resume.txt.</remarks>
        async Task ConfigChangedAsync(AsyncLockedObject<Status> _)
        {
            var newConfig = m_ConfigService.Current;
            foreach (var storageFolder in newConfig.StorageFolders)
            {
                bool shouldStall = storageFolder.Path.Replace("\\", "/").Split("/").Contains("stall");
                if (shouldStall)
                {
                    for (; ; )
                    {
                        string resumePath = Path.Combine(storageFolder.Path, "resume.txt");
                        if (File.Exists(resumePath))
                        {
                            break;
                        }

                        await Task.Delay(25);
                    }
                }
            }
        }

        /// <summary>
        /// Callback when an asset is removed (after everything is done, but still have all the locks locked).
        /// </summary>
        /// <param name="removed">Removed asset.</param>
        /// <remarks>Goal of this method is to stall removal when a file named stall_{asset.id} is present in the folder
        /// that contains config.json.</remarks>
        void AssetRemoved(Asset removed)
        {
            string stallFilePath = Path.Combine(m_ConfigService.PersistPath, $"stall_{removed.Id}");
            while (File.Exists(stallFilePath))
            {
                Thread.Sleep(25);
            }
        }

        /// <summary>
        /// Callback when the launch configuration changes (after everything is done, but still have all the locks locked).
        /// </summary>
        /// <param name="launchConfiguration">Changed locked configuration</param>
        void LaunchConfigurationChanged(ObservableObject launchConfiguration)
        {
            string stallFilePath = Path.Combine(m_ConfigService.PersistPath, "stall_currentMissionLaunchConfiguration");
            while (File.Exists(stallFilePath))
            {
                Thread.Sleep(25);
            }
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;
        readonly ConfigService m_ConfigService;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable -> Keep services we are registered to
        readonly AssetsService m_AssetsService;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable -> Keep services we are registered to
        readonly CurrentMissionLaunchConfigurationService m_CurrentMissionLaunchConfigurationService;
    }
}
