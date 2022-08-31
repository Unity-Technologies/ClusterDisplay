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
            PayloadsService payloadsService)
        {
            m_Logger = logger;
            m_ApplicationLifetime = applicationLifetime;
            m_PayloadsService = payloadsService;
        }

        [HttpPost]
        public Task<IActionResult> Post(Command command)
        {
            return command.Type switch
            {
                CommandType.Prepare => OnPrepare((PrepareCommand)command),
                CommandType.Shutdown => OnSutdown((ShutdownCommand)command),
                _ => Task.FromResult<IActionResult>(BadRequest("Unknown command type"))
            };
        }

        /// <summary>
        /// Execute a <see cref="PrepareCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute</param>
        async Task<IActionResult> OnPrepare(PrepareCommand command)
        {
            if (Path.IsPathFullyQualified(command.Path))
            {
                return BadRequest($"{command.Path} is not an absolute path.");
            }

            // Get the payloads
            var payloads = new List<Payload>();
            try
            {
                var payloadsTask = new List<Task<Payload>>();
                foreach (var payloadId in command.PayloadIds)
                {
                    payloadsTask.Add(m_PayloadsService.GetPayload(payloadId, command.PayloadSource));
                }
                await Task.WhenAll(payloadsTask);
            }
            catch (Exception e)
            {
                return Conflict(e);
            }

            // Merge them together into a single list of files
            Payload mergedPayload;
            try
            {
                mergedPayload = Payload.Merge(payloads);
            }
            catch (Exception e)
            {
                return BadRequest(e);
            }

            // Now the real work, prepare the folder
            var result = EnsurePathIsClean(command.Path, mergedPayload);
            if (result != null) return result;
            return await FillFolder(command.Path, mergedPayload, command.PayloadSource);
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
                    m_Logger.LogWarning($"Failed to load fingerprints from {fingerprintsStorage}, will delete the " +
                        $"folder start from scratch: {e}");
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
                        m_Logger.LogWarning($"Failed to load old content of {path}, will continue hopping remaining " +
                            $"files will not cause problems: {e}");

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
                                $"error validating those files.");
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
                return Conflict(e);
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
        Task<IActionResult> FillFolder(string path, Payload incomingPayload, string fileBlobsSource)
        {
            foreach (var payloadFile in incomingPayload.Files)
            {
                string filePath = Path.Combine(path, payloadFile.Path);
                if (System.IO.File.Exists(filePath))
                {
                    // We assume that any file already existing must be equivalent to what we want to get, otherwise
                    // EnsurePathIsClean should have removed it or failed.
                }
            }

            return Task.FromResult<IActionResult>(Ok());
        }

        /// <summary>
        /// Execute a <see cref="PrepareCommand"/>.
        /// </summary>
        /// <param name="command">The command to execute</param>
        Task<IActionResult> OnSutdown(ShutdownCommand command)
        {
            m_ApplicationLifetime.StopApplication();
            return Task.FromResult<IActionResult>(Accepted());
        }

        static readonly char[] k_PathTrimEndChar = new[] { '/', '\\' };

        readonly ILogger<CommandsController> m_Logger;
        readonly IHostApplicationLifetime m_ApplicationLifetime;
        readonly PayloadsService m_PayloadsService;
    }
}
