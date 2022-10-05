using System.Diagnostics;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Unity.ClusterDisplay.MissionControl.HangarBay.Library;
using Unity.ClusterDisplay.MissionControl.HangarBay.Services;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Controllers
{
    [ApiController]
    [Route("api/v1/commands")]
    public class CommandsController: ControllerBase
    {
        public CommandsController(ILogger<CommandsController> logger, IHostApplicationLifetime applicationLifetime,
            HttpClient httpClient, PayloadsService payloadsService, FileBlobCacheService fileBlobCacheService)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;
            m_HttpClient = httpClient;
            m_PayloadsService = payloadsService;
            m_FileBlobCacheService = fileBlobCacheService;
        }

        [HttpPost]
        public Task<IActionResult> Post(Command command)
        {
            return command.Type switch
            {
                CommandType.Prepare => OnPrepare((PrepareCommand)command),
                CommandType.Restart => OnRestart((RestartCommand)command),
                CommandType.Shutdown => OnShutdown(),
                CommandType.Upgrade => OnUpgrade((UpgradeCommand)command),
                _ => Task.FromResult<IActionResult>(BadRequest("Unknown command type"))
            };
        }

        /// <summary>
        /// Execute a <see cref="PrepareCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute</param>
        async Task<IActionResult> OnPrepare(PrepareCommand command)
        {
            if (!Path.IsPathFullyQualified(command.Path))
            {
                return BadRequest($"{command.Path} is not an absolute path.");
            }

            try
            {
                // Get the payloads
                var payloadsTask = new List<Task<Payload>>();
                foreach (var payloadId in command.PayloadIds)
                {
                    payloadsTask.Add(m_PayloadsService.GetPayload(payloadId, command.PayloadSource));
                }
                await Task.WhenAll(payloadsTask);
                IEnumerable<Payload> payloads = payloadsTask.Select(pt => pt.Result);

                // Merge them together into a single list of files
                Payload mergedPayload;
                try
                {
                    mergedPayload = Payload.Merge(payloads);
                }
                catch (Exception e)
                {
                    return BadRequest(e.ToString());
                }

                // Now the real work, prepare the folder
                var result = EnsurePathIsClean(command.Path, mergedPayload);
                if (result != null) return result;
                result = await FillFolder(command.Path, mergedPayload, command.PayloadSource);
                if (result != null) return result;

                // And last step save it's state
                var fingerprints = FolderContentFingerprints.BuildFrom(command.Path, mergedPayload);
                var fingerprintsStorage = command.Path.TrimEnd(k_PathTrimEndChar) + ".fingerprints.json";
                fingerprints.SaveTo(fingerprintsStorage);

                // We are done
                return Ok();
            }
            catch (HttpRequestException e)
            {
                return StatusCode((int)e.StatusCode!, e.ToString());
            }
            catch (Exception e)
            {
                return Conflict(e.ToString());
            }
        }

        /// <summary>
        /// Ensure that the given path is clean and ready to receive the files of the merged payload we have to prepare.
        /// </summary>
        /// <param name="path">Path of the folder.</param>
        /// <param name="incomingPayload">Files that will be copied in <paramref name="path"/>.</param>
        /// <remarks><c>null</c> if everything succeeded or an <see cref="IActionResult"/> in case of an error.</remarks>
        IActionResult? EnsurePathIsClean(string path, Payload incomingPayload)
        {
            // Load the fingerprint of the last time we prepared to that folder
            FolderContentFingerprints? fingerprints = null;
            var fingerprintsStorage = path.TrimEnd(k_PathTrimEndChar) + ".fingerprints.json";
            if (System.IO.File.Exists(fingerprintsStorage) && Directory.Exists(path))
            {
                try
                {
                    fingerprints = FolderContentFingerprints.LoadFrom(fingerprintsStorage);
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed to load fingerprints from {FingerprintsStorage}, will delete " +
                        "the folder start from scratch", fingerprintsStorage);
                }
            }

            // Deal with missing fingerprints or brand new folders
            if (fingerprints == null)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch (Exception e)
                    {
                        m_Logger.LogWarning(e, "Failed to load old content of {Path}, will continue hopping " +
                            $"remaining files will not cause problems", path);

                        // Are any of the files left part of incomingPayload?  If so this is a major error and we cannot
                        // continue...
                        try
                        {
                            foreach (var payloadFile in incomingPayload.Files)
                            {
                                if (System.IO.File.Exists(Path.Combine(path, payloadFile.Path)))
                                {
                                    return Conflict($"There was an error deleting {payloadFile.Path}.");
                                }
                            }
                        }
                        catch (Exception)
                        {
                            return Conflict($"There was an error deleting some of the files in {path} followed by an " +
                                $"error validating if those files could be tolerated.");
                        }
                    }
                    return null; // We deleted everything (or everything we could), nothing more we can clean...
                }
                else
                {
                    Directory.CreateDirectory(path);
                    return null; // Brand new folder, no need to clean anything further!
                }
            }

            // Cleanup
            try
            {
                fingerprints.PrepareForPayload(path, incomingPayload, m_Logger);
            }
            catch (Exception e)
            {
                return Conflict(e.ToString());
            }

            // Done
            return null;
        }

        /// <summary>
        /// Copy missing files from the payload to the folder.
        /// </summary>
        /// <param name="path">Path of the folder.</param>
        /// <param name="incomingPayload">Files to be copied in <paramref name="path"/>.</param>
        /// <param name="fileBlobsSource">Where to get missing file blobs from.</param>
        /// <returns>Error result or <c>null</c> if everything worked as expected.</returns>
        async Task<IActionResult?> FillFolder(string path, Payload incomingPayload, string fileBlobsSource)
        {
            var copyTasks = new List<Task>();
            foreach (var payloadFile in incomingPayload.Files)
            {
                // We assume that any file already existing must be equivalent to what we want to get, otherwise
                // EnsurePathIsClean should have removed it or failed (and this method shouldn't have been called).
                string filePath = Path.Combine(path, payloadFile.Path);
                if (!System.IO.File.Exists(filePath))
                {
                    try
                    {
                        copyTasks.Add(
                            m_FileBlobCacheService.CopyFileTo(payloadFile.FileBlob, filePath, fileBlobsSource));
                    }
                    catch (Exception e)
                    {
                        return Conflict(e.ToString());
                    }
                }
            }

            await Task.WhenAll(copyTasks);

            return null;
        }

        /// <summary>
        /// Execute a <see cref="ShutdownCommand"/>.
        /// </summary>
        Task<IActionResult> OnShutdown()
        {
            m_ApplicationLifetime.StopApplication();
            return Task.FromResult<IActionResult>(Accepted());
        }

        /// <summary>
        /// Execute a <see cref="RestartCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute</param>
        Task<IActionResult> OnRestart(RestartCommand command)
        {
            var fullPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (fullPath == null)
            {
                throw new NullReferenceException("Failed getting current process path.");
            }
            string startupFolder = Path.GetDirectoryName(fullPath)!;
            string filename = Path.GetFileName(fullPath);

            ProcessStartInfo startInfo = new();
            startInfo.Arguments = GetCurrentProcessArguments();
            startInfo.FileName = filename;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;
            startInfo.WorkingDirectory = startupFolder;

            RemoteManagement.StartAndWaitForThisProcess(startInfo, command.TimeoutSec);

            m_ApplicationLifetime.StopApplication();
            return Task.FromResult<IActionResult>(Accepted());
        }

        /// <summary>
        /// Execute a <see cref="UpgradeCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        async Task<IActionResult> OnUpgrade(UpgradeCommand command)
        {
            var thisProcessFullPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (thisProcessFullPath == null)
            {
                throw new NullReferenceException("Failed getting current process path.");
            }
            string thisProcessDirectory = Path.GetDirectoryName(thisProcessFullPath)!;
            string thisProcessFilename = Path.GetFileName(thisProcessFullPath);
            string setupDirectory = Path.GetFullPath(Path.Combine(thisProcessDirectory, "..", k_InstallSubfolder));

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
                return Conflict($"Error preparing installation folder {setupDirectory}: {e}");
            }

            // Download and unzip
            try
            {
                var response = await m_HttpClient.GetAsync(command.NewVersionUrl);
                response.EnsureSuccessStatusCode();
                using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(setupDirectory, entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath);
                }
            }
            catch (Exception e)
            {
                return Conflict($"Error preparing installation from {command.NewVersionUrl} to {setupDirectory}: {e}");
            }

            // Launch the upgrade process
            ProcessStartInfo startInfo = new();
            startInfo.Arguments = GetCurrentProcessArguments();
            startInfo.FileName = thisProcessFilename;
            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;
            startInfo.WorkingDirectory = setupDirectory;
            RemoteManagement.UpdateThisProcess(startInfo, thisProcessDirectory, command.TimeoutSec);

            // Initiate our termination
            m_ApplicationLifetime.StopApplication();
            return Accepted();
        }

        /// <summary>
        /// Returns a string from the command line arguments used to launch this process.
        /// </summary>
        static string GetCurrentProcessArguments()
        {
            var arguments = RemoteManagement.FilterCommandLineArguments(Environment.GetCommandLineArgs()).Skip(1);
            return RemoteManagement.AssembleCommandLineArguments(arguments);
        }

        static readonly char[] k_PathTrimEndChar = new[] { '/', '\\' };
        const string k_InstallSubfolder = "install";

        readonly ILogger<CommandsController> m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;
        readonly HttpClient m_HttpClient;
        readonly PayloadsService m_PayloadsService;
        readonly FileBlobCacheService m_FileBlobCacheService;
    }
}
