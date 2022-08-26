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
        /// <param name="fileBlobCache">Object that contains the keep up to date with referenced file blobs.</param>
        public PayloadsManager(ILogger logger, string storage, IFileBlobCache fileBlobCache)
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
        /// <remarks>Func's Guid is the <see cref="Payload"/>'s identifier while the object is a cookie received by
        /// GetPayload and passed to this method.  Returns a <see cref="Task"/> that is to be completed when fetch is
        /// completed.
        /// </remarks>
        public Func<Guid, object?, Task<Payload>> FetchFileCallback { get; set; } =
            (Guid _, object? _) => Task.FromResult<Payload>(new Payload());

        /// <summary>
        /// Returns the <see cref="Payload"/> with the given identifier.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <param name="fetchCookie">Cookie passed to <see cref="FetchFileCallback"/> if we need to fetch the
        /// <see cref="Payload"/>.</param>
        public Task<Payload> GetPayload(Guid payloadIdentifier, object? fetchCookie)
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
                var fileNameWOExtension = Path.GetFileNameWithoutExtension(file);
                Guid payloadIdentifier;
                try
                {
                    payloadIdentifier = Guid.Parse(fileNameWOExtension);
                }
                catch (Exception)
                {
                    m_Logger.LogWarning($"Unexpected filename ({fileNameWOExtension}.json) encountered while loading " +
                        $"previous payloads.  Will skip it.");
                    continue;
                }

                // Load .json file
                Payload payload;
                try
                {
                    using (var loadStream = File.Open(file, FileMode.Open))
                    {
                        var deserialized = JsonSerializer.Deserialize<Payload>(loadStream);
                        payload = deserialized ?? throw new NullReferenceException("Deserialize returned null");
                    }
                }
                catch (Exception)
                {
                    m_Logger.LogWarning($"Failed loading back {fileNameWOExtension}.json, will have to be re-fetched.");
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
                catch (Exception)
                {
                    // Something fail, try to rollback
                    foreach (var toDecrease in ((IEnumerable<Guid>)usageCountIncreased).Reverse())
                    {
                        m_FileBlobCache.DecreaseUsageCount(toDecrease);
                    }

                    m_Logger.LogWarning($"There was a problem processing files of {fileNameWOExtension}.json, will have to be re-fetched.");
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
                    ExceptionDispatchInfo.Capture(ae.InnerException!).Throw();
                    throw new Exception(); // Should never reach this code as the above should throw...
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
        IFileBlobCache m_FileBlobCache;

        /// <summary>
        /// Object to lock to synchronize access to m_Payloads.
        /// </summary>
        object m_Lock = new object();

        /// <summary>
        /// Dictionary of all the <see cref="PayloadInfo"/> we know about.
        /// </summary>
        Dictionary<Guid, Task<Payload>> m_Payloads = new();
    }
}
