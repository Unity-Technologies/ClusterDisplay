using System;
using System.Diagnostics;
using System.Text;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Helper functions to help implementing remote management capabilities of the different mission control
    /// processes.
    /// </summary>
    public class RemoteManagement
    {
        /// <summary>
        /// Callback called before starting to copy files for an upgrade.
        /// </summary>
        /// <remarks>Delegate's first <see cref="string"/> is the path to the folder containing the files that will be
        /// copied to the installation path (delegate's second <see cref="string"/> parameter).</remarks>
        public Action<string, string> PreUpgradeCopy { get; set; } = (_, _) => { };

        /// <summary>
        /// Callback called after copy of files for an upgrade.
        /// </summary>
        /// <remarks>Delegate's first <see cref="string"/> is the path to the folder containing the files that have been
        /// copied to the installation path (delegate's second <see cref="string"/> parameter).</remarks>
        public Action<string, string> PostUpgradeCopy { get; set; } = (_, _) => { };

        /// <summary>
        /// Method to be called as soon as possible as part of the service's (HangarBay, LaunchPad, ...) startup to
        /// allow to do the work necessary to allow proper remote management.
        /// </summary>
        /// <returns>Show the service continue its startup (true) or abort (false).</returns>
        public bool InterceptStartup()
        {
            var args = Environment.GetCommandLineArgs();
            for (int argIdx = 0; argIdx < args.Length; ++argIdx)
            {
                var arg = args[argIdx];
                if (arg == "--waitForProcess")
                {
                    if (argIdx + 3 >= args.Length)
                    {
                        DisplayWaitForProcessHelp();
                        return false;
                    }
                    int processId;
                    string semaphoreName;
                    int waitSeconds;
                    try
                    {
                        processId = Convert.ToInt32(args[argIdx + 1]);
                        semaphoreName = args[argIdx + 2];
                        waitSeconds = Convert.ToInt32(args[argIdx + 3]);
                    }
                    catch
                    {
                        DisplayWaitForProcessHelp();
                        return false;
                    }
                    if (!HandleWaitForProcess(processId, semaphoreName, waitSeconds))
                    {
                        return false;
                    }
                    argIdx += 3;
                }
                else if (arg == "--upgrade")
                {
                    if (argIdx + 1 >= args.Length)
                    {
                        DisplayUpgradeHelp();
                        return false;
                    }
                    string toFolder = args[argIdx + 1];
                    HandleUpgrade(toFolder);

                    // If upgrade fail then we want to stop (since we have no idea of the state of the destination
                    // folder).
                    // If upgrade is successful, we also want to stop as we started the process in the new install
                    // folder.
                    // So in both cases we return false.
                    return false;
                }
                else if (arg == "--cleanPath")
                {
                    if (argIdx + 1 >= args.Length)
                    {
                        DisplayCleanPathHelp();
                        return false;
                    }
                    string toClean = args[argIdx + 1];
                    CleanPath(toClean);

                    ++argIdx;
                }
            }
            return true;
        }

        /// <summary>
        /// Starts the specified process supporting <see cref="RemoteManagement"/> and configure it to wait for this
        /// process to be terminated before actually starting.
        /// </summary>
        /// <param name="startInfo">Startup information as used by <see cref="Process.Start(ProcessStartInfo)"/>.
        /// </param>
        /// <param name="maxWaitSeconds">Maximum number of seconds the other process will wait for this process to exit
        /// before killing it.  This process will wait twice that amount for it to give some feedback.</param>
        /// <remarks><paramref name="startInfo"/> will be modified.</remarks>
        public static void StartAndWaitForThisProcess(ProcessStartInfo startInfo, int maxWaitSeconds)
        {
            string semaphoreName = Guid.NewGuid().ToString();
            var semaphore = new Semaphore(0, 1, semaphoreName);

            startInfo.Arguments = $"--waitForProcess {Process.GetCurrentProcess().Id} {semaphoreName} {maxWaitSeconds} " +
                startInfo.Arguments;
            using var process = Process.Start(startInfo);

            if (!semaphore.WaitOne(TimeSpan.FromSeconds(maxWaitSeconds * 2)))
            {
                throw new TimeoutException($"The other process did not give any feedback within {maxWaitSeconds * 2} seconds.");
            }
        }

        /// <summary>
        /// Starts the specified process as an upgrade of the current process.
        /// </summary>
        /// <param name="startInfo">Startup information pointing to the path that contains the upgraded process.</param>
        /// <param name="upgradePath">Path of where to put the upgraded files.</param>
        /// <param name="maxWaitSeconds">Maximum number of seconds the other process will wait for this process to exit
        /// before killing it.  This process will wait twice that amount for it to give some feedback.</param>
        public static void UpdateThisProcess(ProcessStartInfo startInfo, string upgradePath, int maxWaitSeconds)
        {
            startInfo.Arguments = $"--upgrade \"{upgradePath}\" " + startInfo.Arguments;
            StartAndWaitForThisProcess(startInfo, maxWaitSeconds);
        }

        /// <summary>
        /// Remove command line arguments from the provided list to only keep the ones that are not related to remote
        /// management.
        /// </summary>
        /// <param name="toFiler">Command line arguments to filter.</param>
        public static IEnumerable<string> FilterCommandLineArguments(string[] toFiler)
        {
            List<string> ret = new List<string>(toFiler.Length);
            for (int argIdx = 0; argIdx < toFiler.Length; ++argIdx)
            {
                var arg = toFiler[argIdx];
                if (arg == "--waitForProcess")
                {
                    if (argIdx + 3 >= toFiler.Length)
                    {
                        break;
                    }
                    argIdx += 3;
                }
                else if (arg == "--upgrade" || arg == "--cleanPath")
                {
                    if (argIdx + 1 >= toFiler.Length)
                    {
                        break;
                    }
                    ++argIdx;
                }
                else
                {
                    ret.Add(arg);
                }
            }
            return ret;
        }

        /// <summary>
        /// Assemble individual command line arguments into a list of command line arguments ready to be used to spawn
        /// a new process.
        /// </summary>
        /// <param name="arguments">The arguments</param>
        public static string AssembleCommandLineArguments(IEnumerable<string> arguments)
        {
            StringBuilder commandLineBuilder = new();
            foreach (var arg in arguments)
            {
                if (commandLineBuilder.Length > 0)
                {
                    commandLineBuilder.Append(' ');
                }
                if (arg.Contains(' '))
                {
                    commandLineBuilder.Append('"');
                    commandLineBuilder.Append(arg);
                    commandLineBuilder.Append('"');
                }
                else
                {
                    commandLineBuilder.Append(arg);
                }
            }
            return commandLineBuilder.ToString();
        }

        /// <summary>
        /// Display help on how to use the restart option.
        /// </summary>
        static void DisplayWaitForProcessHelp()
        {
            Console.WriteLine("waitForProcess command line option (--waitForProcess) must be followed by the following arguments: ");
            Console.WriteLine("- Process identifier of the process to wait on before starting.");
            Console.WriteLine("- Semaphore name used to synchronize processes.");
            Console.WriteLine("- Maximum wait time in seconds.");
        }

        /// <summary>
        /// Will wait for the process with the given process identifier to be finished before continuing.
        /// </summary>
        /// <param name="processId">Process identifier.</param>
        /// <param name="semaphoreName">Name of the semaphore to release as soon as we start to wait.</param>
        /// <param name="waitSeconds">Maximum number of seconds to wait after the process (before killing it).</param>
        /// <returns>Success</returns>
        static bool HandleWaitForProcess(int processId, string semaphoreName, int waitSeconds)
        {
            // Implementation remarks: Since the following sequence of event is possible:
            // 1. This process (B) take a long time to start
            // 2. Process asking us to start in wait mode (A) has time to exit
            // 3. Another unrelated process (C) start with the same process id as A
            // 4. This start to wait on C instead of A since A is finished and C start with the same identifier before
            //    we had the time to do anything.
            // To avoid the above, process A will be waiting we signal it we are ready before exiting.

            // Get the process object from the process identifier (so we will be ok if another process start with the
            // same process identifier)
            try
            {
                using var toWaitOn = Process.GetProcessById(processId);

                // Signal we are ready
                try
                {
                    var semaphore = new Semaphore(0, 1, semaphoreName);
                    semaphore.Release();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to get semaphore to signal {semaphoreName}: {e}");
                    return false;
                }

                // Wait for the process to exit
                if (!toWaitOn.WaitForExit(waitSeconds * 1000))
                {
                    Console.WriteLine($"Process {processId} did not exit after {waitSeconds} seconds, it will have to be " +
                        $"terminated.");
                    toWaitOn.Kill();
                }

                // We are done waiting
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to find process with identifier {processId}: {e}");
                return false;
            }
        }

        /// <summary>
        /// Display help on how to use the upgrade option.
        /// </summary>
        static void DisplayUpgradeHelp()
        {
            Console.WriteLine("update command line option (--upgrade) must be followed by the folder in which to put the upgraded files.");
        }

        /// <summary>
        /// Upgrade the installation in the given folder from the binaries in the folder of this assembly.
        /// </summary>
        /// <param name="toFolder">Folder in which to put the upgraded files.</param>
        /// <returns>Success</returns>
        void HandleUpgrade(string toFolder)
        {
            string thisProcessFullPath = Process.GetCurrentProcess().MainModule!.FileName!;
            string thisProcessDirectory = Path.GetDirectoryName(thisProcessFullPath)!;
            string thisProcessFilename = Path.GetFileName(thisProcessFullPath);

            // Prepare upgrade
            try
            {
                PreUpgradeCopy(thisProcessDirectory, toFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to prepare upgrade: {e}");
                return;
            }

            // Clean destination folder
            try
            {
                if (Directory.Exists(toFolder))
                {
                    Directory.Delete(toFolder, true);
                }
                Directory.CreateDirectory(toFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to clean upgrade folder {toFolder}: {e}");
                return;
            }

            // Copy files
            try
            {
                CopyDirectory(thisProcessDirectory, toFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to copy installation files from {thisProcessDirectory} to {toFolder}: {e}");
                return;
            }

            // Conclude upgrade
            try
            {
                PostUpgradeCopy(thisProcessDirectory, toFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to conclude upgrade: {e}");
                return;
            }

            // Restart from the new installed folder (and ask to clean the install files)
            ProcessStartInfo startInfo = new();
            var arguments = FilterCommandLineArguments(Environment.GetCommandLineArgs()).Skip(1);
            startInfo.Arguments = $"--cleanPath \"{thisProcessDirectory}\" {AssembleCommandLineArguments(arguments)}";
            startInfo.FileName = thisProcessFilename;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;
            startInfo.WorkingDirectory = toFolder;
            // We should not have started anything yet, so a timeout of 20 seconds is more than enough.
            const int restartTimeout = 20;
            StartAndWaitForThisProcess(startInfo, restartTimeout);
        }

        /// <summary>
        /// Recursively copy files from <paramref name="sourceDir"/> to <paramref name="destinationDir"/>.
        /// </summary>
        /// <param name="sourceDir">Path to the directory containing the files to copy.</param>
        /// <param name="destinationDir">Path to directory to which to copy the files.</param>
        /// <exception cref="DirectoryNotFoundException">Can't find <paramref name="sourceDir"/>.</exception>
        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            Directory.CreateDirectory(destinationDir);

            // Copy files
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // Recursive call
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        /// <summary>
        /// Display help on how to use the cleanPath option.
        /// </summary>
        static void DisplayCleanPathHelp()
        {
            Console.WriteLine("CleanPath command line option (--cleanPath) must be followed by the folder to delete.");
        }

        /// <summary>
        /// Copy files in <paramref name="toClean"/> (and delete the folder).
        /// </summary>
        /// <param name="toClean">Path to the folder to delete.</param>
        /// <remarks>Just do nothing if folder does not already exist and continue (with console warnings) if a file
        /// cannot be erased.</remarks>
        static void CleanPath(string toClean)
        {
            try
            {
                Directory.Delete(toClean, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to delete files from {toClean}: {e}");
            }
        }
    }
}
