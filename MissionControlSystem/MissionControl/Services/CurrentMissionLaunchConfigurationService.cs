using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

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
        public CurrentMissionLaunchConfigurationService(IConfiguration configuration,
            ILogger<CurrentMissionLaunchConfigurationService> logger,
            IHostApplicationLifetime applicationLifetime,
            ObservableObjectCatalogService catalogService,
            ConfigService configService,
            AssetsService assetsService,
            PayloadsService payloadsService,
            FileBlobsService fileBlobsService,
            CapcomUplinkService capcomUplinkService)
            : base(applicationLifetime, catalogService, new LaunchConfiguration(),
                  ObservableObjectsName.CurrentMissionLaunchConfiguration)
        {
            m_Logger = logger;
            m_ConfigService = configService;
            m_AssetsService = assetsService;
            m_PayloadsService = payloadsService;
            m_FileBlobsService = fileBlobsService;
            m_CapcomUplinkService = capcomUplinkService;

            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_CapcomPath = Path.GetFullPath(configuration["capcomFolder"], assemblyFolder!);

            m_PersistPath = Path.Combine(configService.PersistPath, "currentMission/launchConfiguration.json");
            Directory.CreateDirectory(Path.GetDirectoryName(m_PersistPath)!);
            Load();

            applicationLifetime.ApplicationStopping.Register(StopAtExit);

            using var lockedLaunchConfiguration = LockAsync().Result;
            lockedLaunchConfiguration.Value.ObjectChanged += LaunchConfigurationChanged;
            LaunchConfigurationChanged(lockedLaunchConfiguration.Value);
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
                    lockedLaunchConfiguration.Value.DeepCopyFrom(launchConfiguration);
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to load current launch configuration from {Path}, starting from an empty " +
                    "current launch configuration", m_PersistPath);
                throw;
            }
        }

        /// <summary>
        /// Called when the launch configuration changes.
        /// </summary>
        /// <param name="observable">The changed <see cref="LaunchConfiguration"/>.</param>
        /// <remarks>This is executed while everything is locked, but we don't really have an alternative as it would
        /// otherwise mean that we would have a current launch configuration that is not "completely ready" after the
        /// put.  This should however not be too bad as capcom launchables should generally be really small and quick
        /// to start and stop.</remarks>
        void LaunchConfigurationChanged(ObservableObject observable)
        {
            var launchConfiguration = (LaunchConfiguration)observable;
            if (launchConfiguration.AssetId == m_PreparedCapcomAssetId)
            {   // Nothing changed, we are done.
                return;
            }

            // Stop previously running capcom processes
            StopCapcomProcess();

            // Clean any old files in the capcom folder.
            // Remarks: We need to try to delete in a loop as it is possible that the OS keep the folder locked for a
            // little bit longer than the process is running.
            var ellapsedCleaningUp = Stopwatch.StartNew();
            Exception? lastException = null;
            while (Directory.Exists(m_CapcomPath) && ellapsedCleaningUp.Elapsed < TimeSpan.FromSeconds(2))
            {
                try
                {
                    Directory.Delete(m_CapcomPath, true);
                }
                catch (Exception e)
                {
                    // Keep on retrying (after a little break to give some time to the OS to freeup its resources).
                    Thread.Sleep(50);
                    lastException = e;
                }
            }
            if (Directory.Exists(m_CapcomPath))
            {
                m_Logger.LogError(lastException, "Failed to clean folder containing files of previous capcom " +
                    "processes, capcom for the new asset will not be started until the problem is fixed");
                // Put some dummy id so that we try to kill it again next time something is set.
                m_PreparedCapcomAssetId = new Guid();
                return;
            }

            // We are now ready to launch new capcom processes
            using (var lockedCapcomUplink = m_CapcomUplinkService.LockAsync().Result)
            {
                if (!lockedCapcomUplink.Value.IsRunning)
                {
                    lockedCapcomUplink.Value.IsRunning = true;
                    lockedCapcomUplink.Value.SignalChanges();
                }
            }

            // Find the capcom launchables of the asset of the launch configuration.
            if (launchConfiguration.AssetId == Guid.Empty)
            {
                m_PreparedCapcomAssetId = launchConfiguration.AssetId;
                return;
            }
            using (var lockedAssets = m_AssetsService.Manager.GetLockedReadOnlyAsync().Result)
            {
                if (!lockedAssets.Value.TryGetValue(launchConfiguration.AssetId, out var asset))
                {
                    m_Logger.LogWarning("Failed to get asset {AssetId}, will not be able to launch its capcom processes",
                        launchConfiguration.AssetId);
                    // Put some dummy id so that we try to kill it again next time something is set.
                    m_PreparedCapcomAssetId = new Guid();
                    return;
                }

                List<Task> launchTasks = new();
                foreach (var launchable in asset.Launchables)
                {
                    if (launchable.Type == LaunchableBase.CapcomLaunchableType)
                    {
                        CapcomProcess process = new(m_Logger, m_ConfigService, m_PayloadsService, m_FileBlobsService,
                            m_CapcomPath, launchable);
                        launchTasks.Add(process.Launch());
                        m_CapcomProcesses.Add(process);
                    }
                }

                try
                {
                    Task.WhenAll(launchTasks).Wait();
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed to launch some capcom processes");
                    // We still continue as it might be a capcom process that is not essential to the completion of the
                    // mission.
                }

                // Ready to go
                m_PreparedCapcomAssetId = launchConfiguration.AssetId;
            }
        }

        /// <summary>
        /// Stops currently executing capcom processes (if any)
        /// </summary>
        void StopCapcomProcess()
        {
            if (!m_CapcomProcesses.Any())
            {
                return;
            }

            using (var lockedCapcomUplink = m_CapcomUplinkService.LockAsync().Result)
            {
                if (lockedCapcomUplink.Value.IsRunning)
                {
                    lockedCapcomUplink.Value.IsRunning = false;
                    lockedCapcomUplink.Value.SignalChanges();
                }
            }
            try
            {
                var stopDeadline = Task.Delay(m_CapcomProcesses.Max(ccp => ccp.LandingTime));
                List<Task> exitTasks = new();
                foreach (var process in m_CapcomProcesses)
                {
                    exitTasks.Add(process.EnsureDoneBy(stopDeadline));
                }
                Task.WhenAll(exitTasks).Wait();
                m_CapcomProcesses.Clear();
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to stop previously running capcom processes, capcom for the new asset " +
                    "will not be started until the problem is fixed");
                // Put some dummy id so that we try to kill it again next time something is set.
                m_PreparedCapcomAssetId = new Guid();
            }
        }

        /// <summary>
        /// Class managing lifetime of a capcom process (from preparing the files to its end).
        /// </summary>
        class CapcomProcess
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="logger">Logger for error / warnings.</param>
            /// <param name="configService">Configuration service.</param>
            /// <param name="payloadsService">Service from which we fetch the payloads that contains the files necessary
            /// to start the process.</param>
            /// <param name="fileBlobsService">Service from which we fetch the files necessary to start the process.
            /// </param>
            /// <param name="capcomPath">Path in which we store files for the capcom <see cref="Launchable"/>s.</param>
            /// <param name="launchable">Capcom <see cref="Launchable"/> to launch the process of.</param>
            public CapcomProcess(ILogger logger,
                ConfigService configService,
                PayloadsService payloadsService,
                FileBlobsService fileBlobsService,
                string capcomPath,
                Launchable launchable)
            {
                m_Logger = logger;
                m_ConfigService = configService;
                m_PayloadsService = payloadsService;
                m_FileBlobsService = fileBlobsService;
                m_Folder = Path.Combine(capcomPath, launchable.Name);
                m_Payloads = launchable.Payloads.ToList();
                m_LaunchableData = !ReferenceEquals(launchable.Data, null) ?
                    JsonSerializer.Serialize(launchable.Data, Json.SerializerOptions) : "";
                m_PreLaunchPath = launchable.PreLaunchPath;
                m_LaunchPath = launchable.LaunchPath;
                m_LandingTime = launchable.LandingTime;
            }

            /// <summary>
            /// Initiate preparation and launch of the capcom process.
            /// </summary>
            /// <returns>Task that indicate the capcom process is started.</returns>
            public async Task Launch()
            {
                if (!Directory.Exists(m_Folder))
                {
                    Directory.CreateDirectory(m_Folder);
                }

                await PrepareFiles();

                try
                {
                    var prelaunchProcess = Launch(m_PreLaunchPath);
                    if (prelaunchProcess != null)
                    {
                        await prelaunchProcess.WaitForExitAsync();
                    }
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed to execute PreLaunch ({PreLaunchPath}) required to start capcom " +
                        "process", m_PreLaunchPath);
                    // Looking bad, but we might still be able to start the launch process, so let's continue...
                }

                try
                {
                    m_CapcomProcess = Launch(m_LaunchPath);
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed to Launch capcom process ({LaunchPath})", m_LaunchPath);
                }
            }

            /// <summary>
            /// Wait for the process
            /// </summary>
            /// <param name="deadlineTask">Task that when completed indicates the it is too late and we should kill the
            /// capcom process.</param>
            public async Task EnsureDoneBy(Task deadlineTask)
            {
                if (m_CapcomProcess == null)
                {   // No process, nothing to wait for, we are done
                    return;
                }

                var completed = await Task.WhenAny(m_CapcomProcess.WaitForExitAsync(), deadlineTask);
                if (completed == deadlineTask)
                {
                    m_CapcomProcess.Kill(true);
                }
                Debug.Assert(m_CapcomProcess.HasExited);
            }

            /// <summary>
            /// How much time does capcom process needs to realize it has to stop before being killed.
            /// </summary>
            public TimeSpan LandingTime => m_LandingTime;

            /// <summary>
            /// Returns the list of all the files necessary to launch the process
            /// </summary>
            Payload GetMergedPayloads()
            {
                List<Payload> payloads = new();
                foreach (var payloadId in m_Payloads)
                {
                    try
                    {
                        payloads.Add(m_PayloadsService.Manager.GetPayload(payloadId));
                    }
                    catch (Exception e)
                    {
                        m_Logger.LogWarning(e, "Failed to get Payload {Id}", payloadId);
                    }
                }
                return Payload.Merge(payloads);
            }

            /// <summary>
            /// Prepares all the files necessary to launch the process.
            /// </summary>
            Task PrepareFiles()
            {
                var mergedPayloads = GetMergedPayloads();

                List<Task> prepareFileTasks = new();
                foreach (var file in mergedPayloads.Files)
                {
                    prepareFileTasks.Add(PrepareFile(file));
                }
                return Task.WhenAll(prepareFileTasks);
            }

            /// <summary>
            /// Prepare a file necessary to launch the process.
            /// </summary>
            /// <param name="payloadFile">The file to prepare.</param>
            async Task PrepareFile(PayloadFile payloadFile)
            {
                string destPath = Path.Combine(m_Folder, payloadFile.Path);

                var directoryName = Path.GetDirectoryName(destPath)!;
                if (!Directory.Exists(directoryName))
                {
                    try
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    catch
                    {
                        // Chances are failure are simply caused by another task that created the folder before us, so
                        // let's just ignore the exception.
                    }
                }

                using var lockedFileBlob = await m_FileBlobsService.Manager.LockFileBlob(payloadFile.FileBlob);
                await using var compressedStream = File.OpenRead(lockedFileBlob.Path);
                await using GZipStream decompressor = new(compressedStream, CompressionMode.Decompress);
                await using var decompressedStream = File.OpenWrite(destPath);
                await decompressor.CopyToAsync(decompressedStream);
            }

            /// <summary>
            /// Execute the file at the given path
            /// </summary>
            Process? Launch(string path)
            {
                if (path == "")
                {
                    return null;
                }

                ProcessStartInfo processStartInfo = new();
                processStartInfo.WorkingDirectory = m_Folder;
                processStartInfo.FileName = path;
                if (m_LaunchableData != "")
                {
                    processStartInfo.EnvironmentVariables["LAUNCHABLE_DATA"] = m_LaunchableData;
                }
                processStartInfo.EnvironmentVariables["MISSIONCONTROL_CONFIG"] =
                        JsonSerializer.Serialize(m_ConfigService.Current, Json.SerializerOptions);
                ProcessLaunchHelpers.PrepareProcessStartInfo(processStartInfo);
                return Process.Start(processStartInfo);
            }

            readonly ILogger m_Logger;
            readonly ConfigService m_ConfigService;
            readonly PayloadsService m_PayloadsService;
            readonly FileBlobsService m_FileBlobsService;
            readonly TimeSpan m_LandingTime;

            readonly string m_Folder;
            readonly List<Guid> m_Payloads;
            readonly string m_LaunchableData;
            readonly string m_PreLaunchPath;
            readonly string m_LaunchPath;

            Process? m_CapcomProcess;
        }

        /// <summary>
        /// Stop capcom processes at exit
        /// </summary>
        void StopAtExit()
        {
            using (LockAsync().Result)
            {
                LaunchConfiguration fakeLaunchConfiguration = new();
                LaunchConfigurationChanged(fakeLaunchConfiguration);
            }
        }

        readonly ILogger m_Logger;
        readonly ConfigService m_ConfigService;
        readonly AssetsService m_AssetsService;
        readonly PayloadsService m_PayloadsService;
        readonly FileBlobsService m_FileBlobsService;
        readonly CapcomUplinkService m_CapcomUplinkService;

        /// <summary>
        /// Path in which the capcom processes are running.
        /// </summary>
        readonly string m_CapcomPath;

        /// <summary>
        /// Path that stores the list of <see cref="Asset"/>s.
        /// </summary>
        readonly string m_PersistPath;

        /// <summary>
        /// Identifier of the asset for which we currently have all the capcom launchables running.
        /// </summary>
        /// <remarks>Should only be accessed by <see cref="LaunchConfigurationChanged"/> or methods it called (this is
        /// why we do not need synchronizing access to the variable).</remarks>
        Guid m_PreparedCapcomAssetId;

        /// <summary>
        /// Capcom process running for <see cref="m_PreparedCapcomAssetId"/>.
        /// </summary>
        readonly List<CapcomProcess> m_CapcomProcesses = new();
    }
}
