using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    public class CatalogException: Exception
    {
        public CatalogException() { }
        public CatalogException(string message) : base(message) { }
    }

    /// <summary>
    /// Class of the object responsible for managing <see cref="Asset"/>s. (and everything they references).
    /// </summary>
    public class AssetsManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        /// <param name="payloadsManager">Object responsible for managing <see cref="Payload"/>s.</param>
        /// <param name="fileBlobsManager">Object responsible for managing file blobs.</param>
        public AssetsManager(ILogger logger, PayloadsManager payloadsManager, FileBlobsManager fileBlobsManager)
        {
            m_Logger = logger;
            m_PayloadsManager = payloadsManager;
            m_FileBlobsManager = fileBlobsManager;

            m_Assets.OnSomethingChanged += AssetsCollectionModified;
        }

        /// <summary>
        /// Add an asset (and all its content) to the list of assets managed by this object.
        /// </summary>
        /// <param name="assetSource">Object responsible for providing us the different pieces of information about the
        /// <see cref="Asset"/>.</param>
        /// <param name="asset">Information about the asset to add.</param>
        /// <returns>Identifier of the newly created <see cref="Asset"/>.</returns>
        /// <exception cref="CatalogException">There was a problem in the asset's LaunchCatalog.json.</exception>
        /// <exception cref="Exception">Any exception thrown by <paramref name="assetSource"/>.</exception>
        public async Task<Guid> AddAssetAsync(IAssetBase asset, IAssetSource assetSource)
        {
            var catalog = await assetSource.GetCatalogAsync();
            if (catalog.Launchables.Select(l => l.Name).Distinct().Count() < catalog.Launchables.Count())
            {
                throw new CatalogException("Some launchable share the same name, every launchable must have a unique " +
                    "name within the LaunchCatalog.");
            }
            if (catalog.Launchables.Any(l => string.IsNullOrEmpty(l.Name)))
            {
                throw new CatalogException("Launchable name cannot be empty.");
            }

            Dictionary<string, Task<FileBlobInfo>> fileBlobsTask = new();
            List<Task<FileBlobInfo>> fileBlobsTaskList = new();
            Dictionary<Guid, FileBlobInfo> uniqueFileBlobs = new();
            Dictionary<string, Guid> payloads = new();
            List<Guid> payloadsList = new();
            CancellationTokenSource addFileBlobsCancelTokenSource = new();

            try
            {
                // First step add the file blobs (this is most likely the longest step)
                foreach (var payload in catalog.Payloads)
                {
                    foreach (var file in payload.Files)
                    {
                        if (fileBlobsTask.ContainsKey(file.Path))
                        {
                            continue;
                        }

                        var addFileBlobTask = AddFileBlobAsync(assetSource, file, addFileBlobsCancelTokenSource);
                        fileBlobsTask.Add(file.Path, addFileBlobTask);
                        fileBlobsTaskList.Add(addFileBlobTask);
                    }
                }

                // Wait for all the file blobs to be added
                await Task.WhenAll(fileBlobsTaskList);
                Dictionary<string, FileBlobInfo> fileBlobs = new();
                foreach (var fileBlobTaskPair in fileBlobsTask)
                {
                    var fileBlobInfo = await fileBlobTaskPair.Value;
                    fileBlobs[fileBlobTaskPair.Key] = fileBlobInfo;
                    uniqueFileBlobs[fileBlobInfo.Id] = fileBlobInfo;
                }

                // Then, let's add the payloads
                foreach (var catalogPayload in catalog.Payloads)
                {
                    if (payloads.ContainsKey(catalogPayload.Name))
                    {
                        continue;
                    }

                    List<PayloadFile> payloadFiles = new (catalogPayload.Files.Count());
                    foreach (var catalogFile in catalogPayload.Files)
                    {
                        var fileBlob = fileBlobs[catalogFile.Path];
                        payloadFiles.Add(new PayloadFile(catalogFile.Path, fileBlob.Id, fileBlob.CompressedSize, fileBlob.Size));
                    }
                    Payload payload = new(payloadFiles);

                    var payloadIdentifier = Guid.NewGuid();
                    await m_PayloadsManager.AddPayloadAsync(payloadIdentifier, payload);

                    payloads.Add(catalogPayload.Name, payloadIdentifier);
                    payloadsList.Add(payloadIdentifier);
                }

                // And finally, let's update the collection of assets.
                Asset newAsset = new(Guid.NewGuid());
                newAsset.CopyAssetBaseProperties(asset);
                List<Launchable> launchables = new();
                foreach (var catalogLaunchable in catalog.Launchables)
                {
                    Launchable launchable = new();
                    launchable.ShallowCopy(catalogLaunchable);

                    List<Guid> payloadsIds = new();
                    foreach (var catalogPayload in catalogLaunchable.Payloads)
                    {
                        if (payloads.TryGetValue(catalogPayload, out var payloadId))
                        {
                            payloadsIds.Add(payloadId);
                        }
                        else
                        {
                            throw new CatalogException($"Launchable {catalogLaunchable.Name} references payload " +
                                $"{catalogPayload} that cannot be found in LaunchCatalog.json.");
                        }
                    }
                    launchable.Payloads = payloadsIds;

                    launchables.Add(launchable);
                }
                newAsset.Launchables = launchables;
                newAsset.StorageSize = uniqueFileBlobs.Values.Sum(fbi => fbi.CompressedSize);

                using (await m_Lock.LockAsync())
                {
                    try
                    {
                        Debug.Assert(!m_AssetsModificationAllowed);
                        m_AssetsModificationAllowed = true;
                        m_Assets.Add(newAsset);
                    }
                    finally
                    {
                        m_AssetsModificationAllowed = false;
                    }
                }

                // Done
                return newAsset.Id;
            }
            catch
            {
                // Revert
                payloadsList.Reverse();
                foreach (var payloadId in payloadsList)
                {
                    await m_PayloadsManager.RemovePayloadAsync(payloadId);
                }
                throw;
            }
            finally
            {
                addFileBlobsCancelTokenSource.Cancel();

                // Remarks: We always DecreaseFileBlobReferenceAsync on files we added, even when we succeed.
                // m_PayloadsManager.AddPayloadAsync will have increased reference count on the files it uses.
                fileBlobsTaskList.Reverse();
                foreach (var fileBlobTask in fileBlobsTaskList)
                {
                    await Task.WhenAny(fileBlobTask); // WaitAny to avoid throwing if there is an exception
                    if (fileBlobTask.IsCompletedSuccessfully)
                    {
                        await m_FileBlobsManager.DecreaseFileBlobReferenceAsync((await fileBlobTask).Id);
                    }
                }
            }
        }

        /// <summary>
        /// Remove the <see cref="Asset"/> (and everything it references) with the specified identifier.
        /// </summary>
        /// <param name="assetIdentifier"><see cref="Asset"/>'s identifier.</param>
        /// <returns><c>true</c> if remove succeeded or <c>false</c> if there was no asset with that identifier in the
        /// list of <see cref="Asset"/>s of the <see cref="AssetsManager"/>.  Any other problem removing will throw an
        /// exception.</returns>
        public async Task<bool> RemoveAssetAsync(Guid assetIdentifier)
        {
            // First get the asset and remove it from the "known list" so that we do not have to keep m_Lock locked for
            // the whole removal process and so that it appear to be gone ASAP from the outside.
            Asset? asset;
            using (await m_Lock.LockAsync())
            {
                if (!m_Assets.TryGetValue(assetIdentifier, out asset))
                {
                    return false;
                }

                try
                {
                    Debug.Assert(!m_AssetsModificationAllowed);
                    m_AssetsModificationAllowed = true;
                    bool removed = m_Assets.Remove(assetIdentifier);
                    Debug.Assert(removed);
                }
                finally
                {
                    m_AssetsModificationAllowed = false;
                }
            }

            // Remove the payloads, removing the payloads should cascade in removing the files.
            HashSet<Guid> removedPayloads = new();
            foreach (var launchable in asset.Launchables)
            {
                foreach (var payloadId in launchable.Payloads)
                {
                    if (removedPayloads.Add(payloadId))
                    {
                        await m_PayloadsManager.RemovePayloadAsync(payloadId);
                    }
                }
            }

            // Done
            return true;
        }

        /// <summary>
        /// Save the state of the <see cref="AssetsManager"/> to the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="saveTo"><see cref="Stream"/> to save to.</param>
        public async Task SaveAsync(Stream saveTo)
        {
            await using MemoryStream inMemorySerialized = new();

            using (await m_Lock.LockAsync())
            {
                await JsonSerializer.SerializeAsync(inMemorySerialized, m_Assets.Values, Json.SerializerOptions);
            }
            inMemorySerialized.Position = 0;

            await inMemorySerialized.CopyToAsync(saveTo);
        }

        /// <summary>
        /// Loads the state of the <see cref="AssetsManager"/> from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="loadFrom"><see cref="Stream"/> to load from.</param>
        /// <exception cref="InvalidOperationException">If trying to load into a non empty manager.</exception>
        public void Load(Stream loadFrom)
        {
            var assets = JsonSerializer.Deserialize<Asset[]>(loadFrom, Json.SerializerOptions);
            if (assets == null)
            {
                throw new NullReferenceException("Parsing asset array resulted in a null object.");
            }

            // Prepare an incremental update (this is the fastest way to add everything as a single transaction into
            // m_Assets).
            IncrementalCollectionUpdate<Asset> incrementalUpdate = new();
            incrementalUpdate.UpdatedObjects = assets;

            using (m_Lock.Lock())
            {
                if (m_Assets.Count > 0)
                {
                    throw new InvalidOperationException($"Can only call Load on an empty {nameof(AssetsManager)}.");
                }

                try
                {
                    Debug.Assert(!m_AssetsModificationAllowed);
                    m_AssetsModificationAllowed = true;
                    m_Assets.ApplyDelta(incrementalUpdate);
                }
                finally
                {
                    m_AssetsModificationAllowed = false;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="AsyncLockedObject{T}"/> giving access to a
        /// <see cref="IReadOnlyIncrementalCollection{T}"/> that must be disposed ASAP (as it keeps the
        /// <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        public async Task<AsyncLockedObject<IReadOnlyIncrementalCollection<Asset>>> GetLockedReadOnlyAsync()
        {
            return new AsyncLockedObject<IReadOnlyIncrementalCollection<Asset>>(m_Assets,
                await m_Lock.LockAsync());
        }

        /// <summary>
        /// Call the <see cref="FileBlobsManager"/> to add a file blob from the specified file.
        /// </summary>
        /// <param name="assetSource">From where to take the file.</param>
        /// <param name="file">The file to add</param>
        /// <param name="addFileBlobsCancelTokenSource"><see cref="CancellationTokenSource"/> to cancel the task we
        /// call but we also signal it if a add fail so that all other start add stop as soon as possible.</param>
        /// <returns></returns>
        async Task<FileBlobInfo> AddFileBlobAsync(IAssetSource assetSource, LaunchCatalog.PayloadFile file,
            CancellationTokenSource addFileBlobsCancelTokenSource)
        {
            try
            {
                Guid fileBlobId;
                using (var openedFile = await assetSource.GetFileContentAsync(file.Path))
                {
                    byte[] md5Bytes = Convert.FromHexString(file.Md5);
                    var md5 = new Guid(md5Bytes);
                    fileBlobId = await m_FileBlobsManager.AddFileBlobAsync(openedFile.Stream,
                        openedFile.Length, md5, addFileBlobsCancelTokenSource.Token);
                }

                using (var lockedFile = await m_FileBlobsManager.LockFileBlob(fileBlobId))
                {
                    return new(lockedFile.Id, lockedFile.Md5, lockedFile.CompressedSize, lockedFile.Size);
                }
            }
            catch (Exception)
            {
                // Cancel any other FileBlob if one has an error (since the whole partial asset will be aborted and
                // nothing of it kept).
                addFileBlobsCancelTokenSource.Cancel();
                throw;
            }
        }

        /// <summary>
        /// Watchdog to detect if the collection would be modified when not supposed to.
        /// </summary>
        /// <remarks>This is not meant to prevent problems, this is meant to reduce chances it goes unnoticed.</remarks>
        void AssetsCollectionModified(IReadOnlyIncrementalCollection _)
        {
            if (!m_Lock.IsLocked)
            {
                throw new InvalidOperationException($"Modifying {nameof(IncrementalCollection<Asset>)} while " +
                    $"the collection is not locked.");
            }
            if (!m_AssetsModificationAllowed)
            {
                throw new InvalidOperationException($"Looks likes {nameof(IncrementalCollection<Asset>)} from the " +
                    $"{nameof(IReadOnlyIncrementalCollection<Asset>)}.");
            }
        }

        readonly ILogger m_Logger;
        readonly PayloadsManager m_PayloadsManager;
        readonly FileBlobsManager m_FileBlobsManager;

        /// <summary>
        /// Object used to synchronize access to the member variables below.
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// Assets collection
        /// </summary>
        readonly IncrementalCollection<Asset> m_Assets = new();

        /// <summary>
        /// Assuming <see cref="m_Lock"/> is locked by the current thread, is the current thread allowed to modify it?
        /// </summary>
        bool m_AssetsModificationAllowed;
    }
}
