using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Class responsible for managing a list of <see cref="Payload"/> and managing usage count of associated
    /// <see cref="IFileBlobCache"/> FileBlobs.
    /// </summary>
    public class PayloadsManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        /// <param name="storage">Folder in which we will store fetched payloads.</param>
        /// <param name="fileBlobCache">Object that to keep up to date with referenced file blobs.</param>
        public PayloadsManager(ILogger logger, string storage, FileBlobCache fileBlobCache)
        {
            m_Logger = logger;
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_StoragePath = Path.GetFullPath(storage, assemblyFolder!);
            m_FileBlobCache = fileBlobCache;

            Directory.CreateDirectory(m_StoragePath);
            LoadPreviousPayloads();
        }

        /// <summary>
        /// Function called by <see cref="PayloadsManager"/> when we are asked for a new Payload.
        /// </summary>
        /// <remarks>Func <see cref="Guid"/> is the <see cref="Payload"/>'s identifier while the <see cref="object"/>
        /// is a cookie received by <see cref="GetPayload"/> and passed to this method.  Returns a <see cref="Task"/>
        /// that has for result the fetched <see cref="Payload"/>.
        /// </remarks>
        public Func<Guid, object?, Task<Payload>> FetchFileCallback { get; set; } =
            (_, _) => Task.FromResult(new Payload());

        /// <summary>
        /// Returns the <see cref="Payload"/> with the given identifier.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <param name="fetchCookie">Cookie passed to <see cref="FetchFileCallback"/> if we need to fetch the
        /// <see cref="Payload"/>.</param>
        public Task<Payload> GetPayload(Guid payloadIdentifier, object? fetchCookie = null)
        {
            lock (m_Lock)
            {
                if (!m_Payloads.TryGetValue(payloadIdentifier, out var payloadTask))
                {
                    payloadTask = FetchFileCallback(payloadIdentifier, fetchCookie).ContinueWith(
                        tp => FinalizePayloadRegistration(payloadIdentifier, tp));
                    m_Payloads.Add(payloadIdentifier, payloadTask);
                }
                return payloadTask;
            }
        }

        /// <summary>
        /// Method to be called in the constructor to load all <see cref="Payload"/>s fetched in previous executions.
        /// </summary>
        void LoadPreviousPayloads()
        {
            var files = Directory.GetFiles(m_StoragePath, "*.json");
            foreach (var file in files)
            {
                // Parse filename
                var fileNameWoExtension = Path.GetFileNameWithoutExtension(file);
                Guid payloadIdentifier;
                try
                {
                    payloadIdentifier = Guid.Parse(fileNameWoExtension);
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Unexpected filename ({FileNameWoExtension}.json) encountered while loading " +
                        "previous payloads, will skip it", fileNameWoExtension);
                    continue;
                }

                // Load .json file
                Payload payload;
                try
                {
                    using var loadStream = File.Open(file, FileMode.Open);
                    var deserialized = JsonSerializer.Deserialize<Payload>(loadStream);
                    payload = deserialized ?? throw new NullReferenceException("Deserialize returned null");
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed loading back {FileNameWoExtension}.json, will have to be re-fetched",
                        fileNameWoExtension);
                    continue;
                }

                // Increase usage count
                var usageCountIncreased = new List<Guid>();
                try
                {
                    foreach (var payloadFile in payload.Files)
                    {
                        m_FileBlobCache.IncreaseUsageCount(payloadFile.FileBlob, payloadFile.CompressedSize, payloadFile.Size);
                        usageCountIncreased.Add(payloadFile.FileBlob);
                    }
                }
                catch (Exception e)
                {
                    // Something fail, try to rollback
                    foreach (var toDecrease in ((IEnumerable<Guid>)usageCountIncreased).Reverse())
                    {
                        m_FileBlobCache.DecreaseUsageCount(toDecrease);
                    }

                    m_Logger.LogWarning(e, "There was a problem processing files of {FileNameWoExtension}.json, will " +
                        "have to be re-fetched", fileNameWoExtension);
                    continue;
                }

                // Done
                m_Payloads.Add(payloadIdentifier, Task.FromResult(payload));
            }
        }

        /// <summary>
        /// Method that complete processing on a <see cref="Payload"/> once loading is completed.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <param name="fetchTask">Task that finished fetching the payload.</param>
        Payload FinalizePayloadRegistration(Guid payloadIdentifier, Task<Payload> fetchTask)
        {
            // Increase usage count of every file of the Payload
            var usageCountIncreased = new List<Guid>();
            try
            {
                Payload payload;
                try
                {
                    payload = fetchTask.Result;
                }
                catch (AggregateException ae)
                {
                    // We "re-throw" the inner exception of AggregateException generated from the Task infrastructure
                    // so that PayloadsManager.GetPayload can throw the same exception as what was thrown in
                    // FetchFileCallback (otherwise we would have a double AggregateException that does not get
                    // automagically unwrapped by await).
                    ExceptionDispatchInfo.Capture(ae.InnerException!).Throw();

                    // Should never reach this code as the above should throw...  To avoid warnings with code analysis
                    // tools that does not detect it.
                    throw new Exception();
                }

                // Update FileBlobCache usage information
                foreach (var file in payload.Files)
                {
                    m_FileBlobCache.IncreaseUsageCount(file.FileBlob, file.CompressedSize, file.Size);
                    usageCountIncreased.Add(file.FileBlob);
                }

                // Save the payload to disk so that we don't have to reload if when a new PayloadsManager is created.
                using FileStream serializeStream = File.Create(Path.Combine(m_StoragePath, $"{payloadIdentifier}.json"));
                JsonSerializer.Serialize(serializeStream, payload);

                // Done!
                return payload;
            }
            catch (Exception)
            {
                // Something fail, try to rollback
                foreach (var toDecrease in ((IEnumerable<Guid>)usageCountIncreased).Reverse())
                {
                    m_FileBlobCache.DecreaseUsageCount(toDecrease);
                }

                // And remove the task from the dictionary so that asking for the payload again have chance of succeeded
                // (in case it was an intermittent problem).
                lock (m_Lock)
                {
                    m_Payloads.Remove(payloadIdentifier);
                }

                throw;
            }
        }

        /// <summary>
        /// Object to use for logging
        /// </summary>
        ILogger m_Logger;

        /// <summary>
        /// Full path to the folder from which we load and to which we save fetched payloads.
        /// </summary>
        string m_StoragePath;

        /// <summary>
        /// Object that contains the keep up to date with referenced file blobs.
        /// </summary>
        FileBlobCache m_FileBlobCache;

        /// <summary>
        /// Object to lock to synchronize access to m_Payloads.
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// Dictionary of all the <see cref="Payload"/> we know about.
        /// </summary>
        Dictionary<Guid, Task<Payload>> m_Payloads = new();
    }
}
