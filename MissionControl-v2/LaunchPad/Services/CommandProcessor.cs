using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Services
{
    public static class CommandProcessorExtension
    {
        public static void AddCommandProcessor(this IServiceCollection services)
        {
            services.AddSingleton<CommandProcessor>();
        }
    }

    /// <summary>
    /// Service taking care of executing commands.
    /// </summary>
    public class CommandProcessor
    {
        public CommandProcessor(IConfiguration configuration, ILogger<CommandProcessor> logger,
            IHostApplicationLifetime applicationLifetime, HttpClient httpClient,
            StatusService statusService, ConfigService configService)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;
            m_HttpClient = httpClient;
            m_StatusService = statusService;
            m_ConfigService = configService;

            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_LaunchPath = Path.GetFullPath(configuration["launchFolder"], assemblyFolder!);
            try
            {
                Directory.CreateDirectory(m_LaunchPath);
            }
            catch (Exception e)
            {
                m_Logger.LogCritical(e, "Failed to ensure {LaunchFolder} exists", m_LaunchPath);
            }
            m_HttpClient = httpClient;

            m_ConfigService.ValidateNew += ValidateNewConfiguration;

            m_ApplicationLifetime.ApplicationStopping.Register(ApplicationShutdown);
        }

        /// <summary>
        /// Method to be called by Controllers to execute a new received command.
        /// </summary>
        /// <param name="command">Command to process.</param>
        /// <returns>Result of the REST call to process a command.</returns>
        public Task<IActionResult> ProcessCommandAsync(Command command) => command switch
        {
            PrepareCommand commandOfType => Prepare(commandOfType),
            LaunchCommand commandOfType => Task.FromResult(Launch(commandOfType)),
            AbortCommand commandOfType => Abort(commandOfType),
            ClearCommand commandOfType => Task.FromResult(Clear(commandOfType)),
            ShutdownCommand commandOfType => Task.FromResult(Shutdown(commandOfType)),
            RestartCommand commandOfType => Task.FromResult(Restart(commandOfType)),
            UpgradeCommand commandOfType => Task.FromResult(Upgrade(commandOfType)),
            _ => throw new ArgumentException()
        };

        /// <summary>
        /// Validate a new <see cref="ConfigService"/>'s configuration.
        /// </summary>
        /// <param name="newConfig">Information about the new configuration</param>
        static void ValidateNewConfiguration(ConfigService.ConfigChangeSurvey newConfig)
        {
            // Validate ClusterNetworkNic (since we are the closest thing of a user for this parameter as we are passing
            // it to the launched processes).
            var clusterNetworkNic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                ni => ni.Name == newConfig.Proposed.ClusterNetworkNic);
            if (clusterNetworkNic == null)
            {
                IPAddress parsedAddress;
                try
                {
                    parsedAddress = IPAddress.Parse(newConfig.Proposed.ClusterNetworkNic);
                }
                catch (Exception)
                {
                    newConfig.Reject($"ClusterNetworkNic {newConfig.Proposed.ClusterNetworkNic}: does not appear to " +
                        $"be a network interface name or IP address (failed to parse address).");
                    return;
                }

                // Can't find through the name, is it one of the IPs?
                bool found = false;
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (var unicastAddress in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastAddress.Address.Equals(parsedAddress))
                        {
                            found = true; break;
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
                if (!found)
                {
                    newConfig.Reject($"ClusterNetworkNic {newConfig.Proposed.ClusterNetworkNic}: does not appear to " +
                        $"be a network interface name or IP address (failed find a NIC with that IP address).");
                    return;
                }
            }

            // Validate HangarBay's endpoint seem valid (since the command processor is the one that will use it to ask
            // it to prepare payloads).
            try
            {
                _ = new Uri(newConfig.Proposed.HangarBayEndPoint);
            }
            catch (Exception e)
            {
                newConfig.Reject($"{newConfig.Proposed.HangarBayEndPoint}: does not appear to " +
                        $"be a valid URI to contact the HangarBay: {e}");
            }
        }

        /// <summary>
        /// Execute the <see cref="PrepareCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        async Task<IActionResult> Prepare(PrepareCommand command)
        {
            if (!command.PayloadIds.Any())
            {
                return new BadRequestObjectResult("Must specify at least one payload to launch.");
            }
            if (command.LaunchPath.Length == 0)
            {
                return new BadRequestObjectResult("Must specify the path of the executable to launch.");
            }

            Task? waitOnTask = null;
            lock (m_Lock)
            {
                // Fail if there is currently a payload launched
                if (m_StatusService.State == State.Launched)
                {
                    return new ConflictObjectResult("There is already a payload launched, first abort it.");
                }

                // Cancel any already ongoing preparation
                if (m_PreparingTask is {IsCompleted: false})
                {
                    m_PreparingTaskTcs?.Cancel();
                    waitOnTask = m_PreparingTask;
                }

                if (waitOnTask == null)
                {
                    // Nothing to wait on, start task that will begin preparation.
                    m_StatusService.State = State.GettingPayload;
                    m_PreparingTaskTcs = new();
                    m_PreparedParameters = null;
                    m_PreparingTask = Task.Run(() => PrepareAsync(command));
                }
            }
            if (waitOnTask != null)
            {
                await waitOnTask;
                return await Prepare(command);
            }
            return new AcceptedResult();
        }

        /// <summary>
        /// Execute the <see cref="LaunchCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        IActionResult Launch(LaunchCommand command)
        {
            lock (m_Lock)
            {
                // Fail if there is currently a payload launched
                if (m_StatusService.State == State.Launched)
                {
                    return new ConflictObjectResult("There is already a payload launched, first abort it.");
                }

                // Are we currently preparing a launch?  If so let's just schedule launch immediately after.
                if (m_StatusService.State != State.WaitingForLaunch)
                {
                    m_PendingLaunch = command;
                    return new AcceptedResult();
                }

                if (m_PreparedParameters == null)
                {
                    return new ConflictObjectResult("State conflict, missing launch parameters?!?!?");
                }

                // 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, ignition... and lift off!!!!
                ProcessStartInfo processStartInfo = new();
                processStartInfo.WorkingDirectory = m_LaunchPath;
                processStartInfo.FileName = m_PreparedParameters.LaunchPath;
                PrepareEnvironmentVariables(processStartInfo, m_PreparedParameters);
                InvokePowershell(processStartInfo);
                try
                {
                    m_LaunchedProcess = Process.Start(processStartInfo);
                    if (m_LaunchedProcess == null)
                    {
                        throw new NullReferenceException("Failed to launch the process.");
                    }

                    m_LaunchedProcess.WaitForExitAsync().ContinueWith(_ => {
                        lock (m_Lock)
                        {
                            m_LaunchedProcess = null;
                            m_StatusService.State = State.Idle;
                        }
                    });

                    return new OkResult();
                }
                catch(Exception e)
                {
                    m_Logger.LogError(e, "Launch of {Process} failed",m_PreparedParameters.LaunchPath);
                    ClearStateToIdle();
                    return new ConflictObjectResult($"Failed to launch {m_PreparedParameters.LaunchPath}: {e}");
                }
                finally
                {
                    m_PreparedParameters = null;
                    m_StatusService.State = State.Launched;
                }
            }
        }

        /// <summary>
        /// Execute the <see cref="AbortCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        // ReSharper disable once UnusedParameter.Local
        async Task<IActionResult> Abort(AbortCommand command)
        {
            Task? toWaitOn = null;
            lock (m_Lock)
            {
                if (m_PreparingTaskTcs == null && m_PreparingTask == null && m_LaunchedProcess == null)
                {
                    Debug.Assert(m_StatusService.State is State.WaitingForLaunch or State.Idle);
                    m_StatusService.State = State.Idle;
                }
                else
                {
                    m_PreparingTaskTcs?.Cancel();
                    toWaitOn = m_PreparingTask;
                    m_LaunchedProcess?.Kill();
                }
            }

            if (toWaitOn != null)
            {
                await toWaitOn;
            }
            return new OkResult();
        }

        /// <summary>
        /// Execute the <see cref="ClearCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        // ReSharper disable once UnusedParameter.Local
        IActionResult Clear(ClearCommand command)
        {
            lock (m_Lock)
            {
                if (m_StatusService.State is not State.WaitingForLaunch and not State.Idle)
                {
                    return new ConflictObjectResult("Launchpad state must be WaitForLaunch or Idle to execute the clear command.");
                }

                m_PreparedParameters = null;
                m_StatusService.State = State.Idle;

                try
                {
                    Directory.Delete(m_LaunchPath, true);
                    Directory.CreateDirectory(m_LaunchPath);
                }
                catch (Exception e)
                {
                    // We cleared the best that we can, at worst if there is a real critical file missing we will get
                    // an error while preparing the launch pad next time.
                    m_Logger.LogWarning(e, "Failed to clear some files from {LaunchPath}", m_LaunchPath);
                }
            }
            return new OkResult();
        }

        /// <summary>
        /// Execute the <see cref="ShutdownCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        // ReSharper disable once UnusedParameter.Local
        IActionResult Shutdown(ShutdownCommand command)
        {
            m_ApplicationLifetime.StopApplication();
            return new AcceptedResult();
        }

        /// <summary>
        /// Execute the <see cref="RestartCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        IActionResult Restart(RestartCommand command)
        {
            lock (m_Lock)
            {
                // Remarks: We could be tempted to stop everything when we are in another state than WaitingForLaunch
                // or Idle to allow the restart command in any state.  However doing so could cause problems if
                // trying to restart during a long or stuck prelaunch sequence.
                if (m_StatusService.State is not State.WaitingForLaunch and not State.Idle)
                {
                    return new ConflictObjectResult("Launchpad state must be WaitForLaunch or Idle to execute the restart command.");
                }

                var fullPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (fullPath == null)
                {
                    throw new NullReferenceException("Failed getting current process path.");
                }
                string startupFolder = Path.GetDirectoryName(fullPath)!;
                string filename = Path.GetFileName(fullPath);
                var arguments = RemoteManagement.FilterCommandLineArguments(Environment.GetCommandLineArgs()).Skip(1);

                ProcessStartInfo startInfo = new();
                startInfo.Arguments = RemoteManagement.AssembleCommandLineArguments(arguments);
                startInfo.FileName = filename;
                startInfo.UseShellExecute = true;
                startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                startInfo.WorkingDirectory = startupFolder;

                RemoteManagement.StartAndWaitForThisProcess(startInfo, command.TimeoutSec);

                m_ApplicationLifetime.StopApplication();
                return new AcceptedResult();
            }
        }

        /// <summary>
        /// Execute the <see cref="UpgradeCommand"/>.
        /// </summary>
        /// <param name="command">Command parameters.</param>
        /// <returns>Result of the command</returns>
        IActionResult Upgrade(UpgradeCommand command)
        {
            lock (m_Lock)
            {
                // Remarks: We could be tempted to stop everything when we are in another state than WaitingForLaunch
                // or Idle to allow the restart command in any state.  However doing so could cause problems if
                // trying to upgrade during a long or stuck prelaunch sequence.
                if (m_StatusService.State is not State.WaitingForLaunch and not State.Idle)
                {
                    return new ConflictObjectResult("Launchpad state must be WaitForLaunch or Idle to execute the upgrade command.");
                }

                var thisProcessFullPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (thisProcessFullPath == null)
                {
                    throw new NullReferenceException("Failed getting current process path.");
                }
                string thisProcessDirectory = Path.GetDirectoryName(thisProcessFullPath)!;
                string thisProcessFilename = Path.GetFileName(thisProcessFullPath);
                string setupDirectory = Path.GetFullPath(Path.Combine(thisProcessDirectory, "..", k_InstallSubfolder));
                var arguments = RemoteManagement.FilterCommandLineArguments(Environment.GetCommandLineArgs()).Skip(1);

                // Clean destination folder
                try
                {
                    if (Directory.Exists(setupDirectory))
                    {
                        Directory.Delete(setupDirectory, true);
                    }
                    Directory.CreateDirectory(setupDirectory);
                }
                catch (Exception e)
                {
                    return new ConflictObjectResult($"Error preparing installation folder {setupDirectory}: {e}");
                }

                // Download and unzip.  Remarks, we use "blocking call".  I know this is not "optimal", but we are about
                // to upgrade (and so restart the process) anyway, so a higher response time is ok...
                try
                {
                    var response = m_HttpClient.GetAsync(command.NewVersionUrl).Result;
                    response.EnsureSuccessStatusCode();
                    using var archive = new ZipArchive(response.Content.ReadAsStreamAsync().Result);
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(setupDirectory, entry.FullName));
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        entry.ExtractToFile(destinationPath);
                    }
                }
                catch (Exception e)
                {
                    return new ConflictObjectResult($"Error preparing installation from {command.NewVersionUrl} to {setupDirectory}: {e}");
                }

                // Launch the upgrade process
                ProcessStartInfo startInfo = new();
                startInfo.Arguments = RemoteManagement.AssembleCommandLineArguments(arguments);
                startInfo.FileName = thisProcessFilename;
                startInfo.UseShellExecute = true;
                startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                startInfo.WorkingDirectory = setupDirectory;
                RemoteManagement.UpdateThisProcess(startInfo, thisProcessDirectory, command.TimeoutSec);

                // Initiate our termination
                m_ApplicationLifetime.StopApplication();
                return new AcceptedResult();
            }
        }

        /// <summary>
        /// Method of the task asynchronously performing launch.
        /// </summary>
        /// <param name="prepareCommand">What to prepare for launch.</param>
        async Task PrepareAsync(PrepareCommand prepareCommand)
        {
            _ = (await GetPayloads(prepareCommand)) &&
                (await ExecutePreLaunch(prepareCommand)) &&
                EnterWaitForLaunch(prepareCommand);
        }

        /// <summary>
        /// Fetch the payloads from the HangarBay.
        /// </summary>
        /// <param name="prepareCommand">What to prepare for launch.</param>
        /// <returns>True success, false failure (don't execute the next steps to prepare launch).</returns>
        async Task<bool> GetPayloads(PrepareCommand prepareCommand)
        {
            HangarBay.PrepareCommand prepareLaunchPadCommand = new();
            prepareLaunchPadCommand.PayloadIds = prepareCommand.PayloadIds;
            prepareLaunchPadCommand.PayloadSource = prepareCommand.PayloadSource;
            prepareLaunchPadCommand.Path = m_LaunchPath;
            Uri uri = new(new Uri(m_ConfigService.Current.HangarBayEndPoint), "api/v1/commands");
            try
            {
                var prepareResult = await m_HttpClient.PostAsJsonAsync(uri, prepareLaunchPadCommand, Json.SerializerOptions,
                    m_PreparingTaskTcs?.Token ?? default);
                prepareResult.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to prepare launchpad files");
                ClearStateToIdle();
                return false;
            }
        }

        /// <summary>
        /// Execute the process to prepare launch.
        /// </summary>
        /// <param name="prepareCommand">What to prepare for launch.</param>
        /// <returns>True success, false failure (don't execute the next steps to prepare launch).</returns>
        async Task<bool> ExecutePreLaunch(PrepareCommand prepareCommand)
        {
            if (prepareCommand.PreLaunchPath.Length <= 0)
            {
                return true;
            }

            m_StatusService.State = State.PreLaunch;

            ProcessStartInfo processStartInfo = new();
            processStartInfo.WorkingDirectory = m_LaunchPath;
            processStartInfo.FileName = prepareCommand.PreLaunchPath;
            PrepareEnvironmentVariables(processStartInfo, prepareCommand);
            InvokePowershell(processStartInfo);
            try
            {
                var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    throw new NullReferenceException("Failed to start prepare process.");
                }
                await process.WaitForExitAsync();
                if (process.ExitCode != 0 || m_PreparingTaskTcs is {IsCancellationRequested: true})
                {
                    if (m_PreparingTaskTcs == null || !m_PreparingTaskTcs.IsCancellationRequested)
                    {
                        m_Logger.LogError("Prepare process ({PrepareProcess}) returned {ExitCode} (not 0)",
                                prepareCommand.PreLaunchPath, process.ExitCode);
                    }
                    ClearStateToIdle();
                    return false;
                }

                return true;
            }
            catch(Exception e)
            {
                m_Logger.LogError(e, "Prepare process ({PrepareProcess}) failed to prepare launchpad files",
                    prepareCommand.PreLaunchPath);
                ClearStateToIdle();
                return false;
            }
        }

        /// <summary>
        /// Enter the waiting for launch state.
        /// </summary>
        /// <param name="prepareCommand">What to prepare for launch.</param>
        /// <returns>True success, false failure (don't execute the next steps to prepare launch).</returns>
        bool EnterWaitForLaunch(PrepareCommand prepareCommand)
        {
            lock (m_Lock)
            {
                m_PreparingTask = null;
                m_PreparingTaskTcs = null;
                m_PreparedParameters = prepareCommand;
                m_StatusService.State = State.WaitingForLaunch;

                if (m_PendingLaunch != null)
                {
                    var launchCommand = m_PendingLaunch;
                    m_PendingLaunch = null;
                    Task.Run(() => Launch(launchCommand));
                }
            }

            return true;
        }

        /// <summary>
        /// Conclude preparation by returning current state to idle.
        /// </summary>
        void ClearStateToIdle()
        {
            lock (m_Lock)
            {
                m_PreparingTask = null;
                m_PreparingTaskTcs = null;
                m_PreparedParameters = null;
                m_StatusService.State = State.Idle;
            }
        }

        /// <summary>
        /// Prepare environment variables to launch a process.
        /// </summary>
        /// <param name="processStartInfo">Information to launch the process.</param>
        /// <param name="prepareCommand">What to prepare for launch (which influence the environment variables we
        /// prepare).</param>
        void PrepareEnvironmentVariables(ProcessStartInfo processStartInfo, PrepareCommand prepareCommand)
        {
            if (!Object.ReferenceEquals(prepareCommand.LaunchData, null))
            {
                processStartInfo.EnvironmentVariables["LAUNCH_DATA"] =
                    JsonSerializer.Serialize(prepareCommand.LaunchData, Json.SerializerOptions);
            }
            processStartInfo.EnvironmentVariables["LAUNCHPAD_CONFIG"] =
                JsonSerializer.Serialize(m_ConfigService.Current, Json.SerializerOptions);
        }

        /// <summary>
        /// Detects if we are trying to run a powershell script (.ps1) and if so modify the ProcessStartInfo to execute
        /// it using powershell.
        /// </summary>
        /// <param name="processStartInfo">Information to launch the process.</param>
        static void InvokePowershell(ProcessStartInfo processStartInfo)
        {
            if (processStartInfo.FileName.EndsWith("ps1"))
            {
                processStartInfo.Arguments = "-File \"" + processStartInfo.FileName + "\" " + processStartInfo.Arguments;
                processStartInfo.FileName = "powershell.exe";
            }
        }

        /// <summary>
        /// Method called when the application is requested to shutdown.
        /// </summary>
        void ApplicationShutdown()
        {
            Task? toWaitOn;
            lock (m_Lock)
            {
                m_PreparingTaskTcs?.Cancel();
                toWaitOn = m_PreparingTask;
                m_LaunchedProcess?.Kill();
            }
            if (toWaitOn != null)
            {
                toWaitOn.Wait();
            }
        }

        const string k_InstallSubfolder = "install";

        readonly ILogger<CommandProcessor> m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;
        readonly HttpClient m_HttpClient;
        readonly StatusService m_StatusService;
        readonly ConfigService m_ConfigService;

        /// <summary>
        /// Folder in which payloads are prepared for launch.
        /// </summary>
        readonly string m_LaunchPath;

        /// <summary>
        /// Used to synchronize access to member variables of this class.
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// Task performing preparation of <see cref="Prepare"/>.
        /// </summary>
        Task? m_PreparingTask;
        /// <summary>
        /// To cancel <see cref="m_PreparingTask"/>.
        /// </summary>
        CancellationTokenSource? m_PreparingTaskTcs;
        /// <summary>
        /// Command prepared on the launchpad, waiting for launch.
        /// </summary>
        PrepareCommand? m_PreparedParameters;

        /// <summary>
        /// Should we launch as soon as preparation is done?
        /// </summary>
        LaunchCommand? m_PendingLaunch;

        /// <summary>
        /// Currently launched process
        /// </summary>
        Process? m_LaunchedProcess;
    }
}
