using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Object responsible for managing a set of payloads.
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global -> Virtual methods used by mock in unit tests
    public class PayloadsManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        /// <param name="storage">Folder in which we will store payloads.</param>
        /// <param name="fileBlobsManager">Object that to keep up to date with referenced file blobs.</param>
        public PayloadsManager(ILogger logger, string storage, FileBlobsManager fileBlobsManager)
        {
            m_Logger = logger;
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_StoragePath = Path.GetFullPath(storage, assemblyFolder!);
            m_FileBlobsManager = fileBlobsManager;

            Directory.CreateDirectory(m_StoragePath);
            LoadPreviousPayloads();
        }

        /// <summary>
        /// Adds a <see cref="Payload"/> to the manager.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <param name="payload">Definition of the <see cref="Payload"/> to add to the manager.</param>
        /// <exception cref="ArgumentException">A <see cref="Payload"/> wit the same identifier is already in the
        /// managed list of <see cref="Payload"/>s.</exception>
        /// <exception cref="KeyNotFoundException">If trying to reference a file blob that is not in the
        /// <see cref="FileBlobsManager"/>.</exception>
        public virtual async Task AddPayloadAsync(Guid payloadIdentifier, Payload payload)
        {
            List<PayloadFile> listOfFilesAdded = new();
            bool addedToCollection = false;
            string filename = "";
            try
            {
                // Increase the reference to each file blobs of the payload
                foreach (var file in payload.Files)
                {
                    m_FileBlobsManager.IncreaseFileBlobReference(file.FileBlob);
                    listOfFilesAdded.Add(file);
                }

                // Add it to the collection
                lock (m_Lock)
                {
                    // This will throw in case of collision and this is exactly what we want
                    m_Payloads.Add(payloadIdentifier, payload);
                    addedToCollection = true;
                }

                // Save the payload file
                filename = GetPayloadFilename(payloadIdentifier);
                await using var serializeStream = File.Create(filename);
                await JsonSerializer.SerializeAsync(serializeStream, payload);
            }
            catch
            {
                if (File.Exists(filename))
                {
                    try
                    {
                        File.Delete(filename);
                    }
                    catch (Exception e)
                    {
                        m_Logger.LogError(e, "Fail to delete incomplete payload file while serializing {PayloadId}",
                            payloadIdentifier);
                    }
                }

                if (addedToCollection)
                {
                    lock (m_Lock)
                    {
                        m_Payloads.Remove(payloadIdentifier);
                    }
                }

                listOfFilesAdded.Reverse();
                foreach (var rollbackFile in listOfFilesAdded)
                {
                    await m_FileBlobsManager.DecreaseFileBlobReferenceAsync(rollbackFile.FileBlob);
                }
                throw;
            }
        }

        /// <summary>
        /// Returns the <see cref="Payload"/> with the given identifier.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <exception cref="KeyNotFoundException">If not payload with the given identifier can be found.</exception>
        public Payload GetPayload(Guid payloadIdentifier)
        {
            lock (m_Lock)
            {
                return m_Payloads[payloadIdentifier];
            }
        }

        /// <summary>
        /// Remove the <see cref="Payload"/> with the given identifier from the manager.
        /// </summary>
        /// <param name="payloadIdentifier"><see cref="Payload"/>'s identifier.</param>
        /// <exception cref="KeyNotFoundException">If not payload with the given identifier can be found.</exception>
        public virtual async Task RemovePayloadAsync(Guid payloadIdentifier)
        {
            // Remove dictionary entry
            Payload payload;
            lock (m_Lock)
            {
                // This will throw if key is missing and this is exactly what we want.
                payload = m_Payloads[payloadIdentifier];
                bool removed = m_Payloads.Remove(payloadIdentifier);
                Debug.Assert(removed);
            }

            // Remove file
            string payloadFilename = GetPayloadFilename(payloadIdentifier);
            try
            {
                File.Delete(payloadFilename);
            }
            catch
            {
                lock (m_Lock)
                {
                    try
                    {
                        m_Payloads.Add(payloadIdentifier, payload);
                    }
                    catch (Exception e)
                    {   // This shouldn't normally happen.  We would need to have someone that add a payload while it
                        // is being removed (and was able to do it between the unlock / lock.  Furthermore, payload
                        // identifiers are guids that should be generated every time, so unique...
                        m_Logger.LogError(e, "Failed to add back dictionary entry for {PayloadId}, state of the list " +
                            "of payloads is now undefined, think of restarting Mission Control", payloadIdentifier);
                        throw;
                    }
                }
                throw;
            }

            // Decrease usage count of every file blobs
            foreach (var file in payload.Files)
            {
                try
                {
                    await m_FileBlobsManager.DecreaseFileBlobReferenceAsync(file.FileBlob);
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Removal of payload {PayloadId} discovered that file blob {FileBlobId} " +
                        "was missing (this is an indicator that some other non removed payloads might also be " +
                        "missing files)", payloadIdentifier, file.FileBlob);
                }
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
                    using var loadStream = File.OpenRead(file);
                    var deserialized = JsonSerializer.Deserialize<Payload>(loadStream);
                    payload = deserialized ?? throw new NullReferenceException("Deserialize returned null");
                }
                catch (Exception e)
                {
                    m_Logger.LogError(e, "Failed loading back {FileNameWoExtension}.json, some files might be missing " +
                        "from some assets", fileNameWoExtension);
                    continue;
                }

                // Increase usage count
                foreach (var payloadFile in payload.Files)
                {
                    try
                    {
                        m_FileBlobsManager.IncreaseFileBlobReference(payloadFile.FileBlob);
                    }
                    catch (Exception e)
                    {
                        m_Logger.LogError(e, "There was a problem increasing usage count of {Path} to {BlobId} of " +
                            "{FileNameWoExtension}.json, let's just hope this is not an important file...",
                            payloadFile.Path, payloadFile.FileBlob, fileNameWoExtension);
                    }
                }

                // Done
                m_Payloads.Add(payloadIdentifier, payload);
            }
        }

        /// <summary>
        /// Returns the save / load path for a payload with the given identifier.
        /// </summary>
        /// <param name="payloadIdentifier">Payloads identifier.</param>
        string GetPayloadFilename(Guid payloadIdentifier)
        {
            return Path.Combine(m_StoragePath, $"{payloadIdentifier}.json");
        }

        /// <summary>
        /// Object to use for logging
        /// </summary>
        readonly ILogger m_Logger;

        /// <summary>
        /// Full path to the folder from which we load and to which we save fetched payloads.
        /// </summary>
        readonly string m_StoragePath;

        /// <summary>
        /// Object that contains the keep up to date with referenced file blobs.
        /// </summary>
        readonly FileBlobsManager m_FileBlobsManager;

        /// <summary>
        /// Object to lock to synchronize access to m_Payloads.
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// Dictionary of all the <see cref="Payload"/> we know about.
        /// </summary>
        readonly Dictionary<Guid, Payload> m_Payloads = new();
    }
}
