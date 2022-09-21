using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Main class responsible for managing the files in the different storage folders.
    /// </summary>
    public class FileBlobCache: IFileBlobCache
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        public FileBlobCache(ILogger logger)
        {
            m_Logger = logger;
        }

        /// <summary>
        /// Function called by <see cref="FileBlobCache"/> when a file is to be fetched.
        /// </summary>
        /// <remarks>Func <see cref="Guid"/> is the file blob identifier of the file to fetch, the
        /// <see cref="string"/> is the path of where to save that fetched content and the <see cref="object"/> is the
        /// cookie received by <see cref="CopyFileToAsync"/>.  Returns a <see cref="Task"/> that is to be completed when
        /// fetch is completed.</remarks>
        public Func<Guid, string, object?, Task> FetchFileCallback { get; set; } = (_, _, _) => Task.CompletedTask;

        /// <summary>
        /// Function called by <see cref="FileBlobCache"/> when asked to copy a file.
        /// </summary>
        /// <remarks>Func first <see cref="string"/> is the path to the file to copy, the second one is the path to the
        /// destination and the <see cref="object"/> is the cookie received by <see cref="CopyFileToAsync"/>.  Returns a
        /// <see cref="Task"/> that is to be completed when copy is finished.</remarks>
        public Func<string, string, object?, Task> CopyFileCallback { get; set; } = (_, _, _) => Task.CompletedTask;

        /// <summary>
        /// Increase usage count of the file with the given file blob identifier.
        /// </summary>
        /// <param name="fileBlobId">File blob identifier.</param>
        /// <param name="compressedSize">Number of bytes of the compressed file blob.</param>
        /// <param name="size">Number of bytes taken by the file blob content.</param>
        /// <exception cref="ArgumentException">If value of <paramref name="compressedSize"/> or <paramref name="size"/>
        /// does not match value of previous calls with the same <paramref name="fileBlobId"/>.</exception>
        public void IncreaseUsageCount(Guid fileBlobId, long compressedSize, long size)
        {
            lock (m_Lock)
            {
                if (m_Files.TryGetValue(fileBlobId, out var fileInfo))
                {
                    // We already knew about that file, just increase usage count
                    if (compressedSize != fileInfo.CompressedSize)
                    {
                        throw new ArgumentException("CompressedSize does not match compressed size of previous entry.  " +
                            "A given fileBlobId should always have the same compressed size.", nameof(compressedSize));
                    }
                    if (size != fileInfo.Size)
                    {
                        throw new ArgumentException("Size does not match size of previous entry.  A given fileBlobId " +
                            "should always have the same size.", nameof(size));
                    }
                    ++fileInfo.ReferenceCount;

                    if (fileInfo.ReferenceCount == 1 && fileInfo.StorageFolder != null)
                    {
                        // We are starting to use (for the first time) a file that is already present in a storage
                        // folder (was probably used by another payload that has been removed).  Move it from the
                        // unreferenced list to the InCache list.  Putting it as the last one to be evicted while we are
                        // still not 100% sure it will be used might not be perfect but it is much simpler (let's keep
                        // it simple for now and we will change that if ever we see it causes problems...).
                        Debug.Assert(fileInfo.NodeInList != null);
                        Debug.Assert(fileInfo.StorageFolder.Unreferenced.IsForThisList(fileInfo.NodeInList));
                        fileInfo.StorageFolder.Unreferenced.Remove(fileInfo.NodeInList);
                        fileInfo.NodeInList = fileInfo.StorageFolder.InCache.AddLast(fileInfo);

                        // Lists of files in the StorageFolder has changed, so it needs to be saved.
                        fileInfo.StorageFolder.NeedSaving = true;
                    }
                }
                else
                {
                    // We don't have this file in any of our storage folders so far, so let's prepare an entry that is 
                    // not yet associated to any storage folder.
                    fileInfo = new CacheFileInfo();
                    fileInfo.Id = fileBlobId;
                    fileInfo.CompressedSize = compressedSize;
                    fileInfo.Size = size;
                    m_Files[fileBlobId] = fileInfo;
                    fileInfo.ReferenceCount = 1;
                }
            }
        }

        /// <summary>
        /// Decrease use count of the file with the given file blob identifier.
        /// </summary>
        /// <param name="fileBlobId">File blob identifier.</param>
        public void DecreaseUsageCount(Guid fileBlobId)
        {
            DecreaseUsageCount(fileBlobId, false);
        }

        /// <summary>
        /// Decrease use count of the file with the given file blob identifier.
        /// </summary>
        /// <param name="fileBlobId">File blob identifier.</param>
        /// <param name="muteDefer">Do we want to mute the warning message about deferring decreasing of usage count?</param>
        void DecreaseUsageCount(Guid fileBlobId, bool muteDefer)
        {
            lock (m_Lock)
            {
                if (!m_Files.TryGetValue(fileBlobId, out var fileInfo))
                {
                    m_Logger.LogWarning("Trying to remove a reference to {FileBlobId} but there is no file with that " +
                        $"identifier, will skip but this is not supposed to happen", fileBlobId);
                    return;
                }
                if (fileInfo.ReferenceCount <= 0)
                {
                    m_Logger.LogWarning("Trying to remove a reference to {FileBlobId} but the file was not already " +
                        $"referenced, will skip but this is not supposed to happen", fileBlobId);
                    return;
                }
                if (fileInfo.ReferenceCount == 1 && (fileInfo.FetchTask != null || fileInfo.CopyTasks.Count > 0))
                {
                    if (!muteDefer)
                    {
                        m_Logger.LogWarning("Unexpected, trying to remove the last usage of {FileBlobId} that is " +
                            $"currently in use, will defer its processing (asynchronously) for when it will not be " +
                            $"used anymore", fileBlobId);
                    }
                    Task.WhenAll(fileInfo.AllTasks).ContinueWith(_ => DecreaseUsageCount(fileBlobId, true));
                    return;
                }
                --fileInfo.ReferenceCount;
                if (fileInfo.ReferenceCount == 0)
                {
                    if (fileInfo.StorageFolder != null)
                    {
                        // Last reference of a file in a storage folder, transfer it to the unreferenced file list.
                        Debug.Assert(fileInfo.NodeInList != null);
                        Debug.Assert(fileInfo.StorageFolder.InCache.IsForThisList(fileInfo.NodeInList));
                        fileInfo.StorageFolder.InCache.Remove(fileInfo.NodeInList);
                        fileInfo.NodeInList = fileInfo.StorageFolder.Unreferenced.AddLast(fileInfo);

                        // List of files in the StorageFolder has changed, so it needs to be saved.
                        fileInfo.StorageFolder.NeedSaving = true;
                    }
                    else
                    {
                        // Last reference of a file that is nowhere, simply remove it from our list of files.
                        m_Files.Remove(fileBlobId);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a storage folder to the cache manager (and add any files it would contains to the "in-memory" indexes).
        /// </summary>
        /// <param name="config">Information about the storage folder.</param>
        /// <remarks>Payloads potentially referencing those files need to be added before calling this method or 
        /// otherwise the last used time of the file will be cleared (since the file will not be referenced).</remarks>
        /// <exception cref="ArgumentException">If asking to add a storage folder we already have or this is a new
        /// storage folder and it is not empty.</exception>
        public void AddStorageFolder(StorageFolderConfig config)
        {
            lock (m_Lock)
            {
                string effectivePath = GetEffectiveStoragePath(config.Path);
                if (m_StorageFolders.ContainsKey(effectivePath))
                {
                    throw new ArgumentException($"{nameof(FileBlobCache)} already contain a StorageFolder with the " +
                        $"path {effectivePath}.", nameof(config));
                }

                // Testing for the folder being already added has been done using m_StorageFolders.ContainsKey, however 
                // it is not bullet proof as there could be some symbolic links involved or different case on a case 
                // insensitive file system.  So let's make a more exhaustive test.
                if (Directory.Exists(effectivePath))
                {
                    string pathToTheSameFolder = FileHelpers.GetPathToTheSameFolder(effectivePath,
                        m_StorageFolders.Values.Select(sf => sf.FullPath));
                    if (pathToTheSameFolder.Length > 0)
                    {
                        throw new ArgumentException($"{nameof(FileBlobCache)} already contain a StorageFolder with " +
                            $"the path {pathToTheSameFolder} that is the equivalent to {config.Path}.", nameof(config));
                    }
                }

                // Load information about the folder
                StorageFolderInfo storageFolderInfo;
                string storageFolderMetadataJson = StorageFolderInfo.GetMetadataFilePath(effectivePath);
                if (File.Exists(storageFolderMetadataJson))
                {
                    StorageFolderInfo? deserialized;
                    using (var loadStream = File.Open(storageFolderMetadataJson, FileMode.Open))
                    {
                        deserialized = JsonSerializer.Deserialize<StorageFolderInfo>(loadStream);
                    }

                    storageFolderInfo = deserialized ?? throw new NullReferenceException(
                        $"Got an unexpected null StorageFolderInfo while de-serializing {storageFolderMetadataJson}.");
                    storageFolderInfo.FullPath = effectivePath;
                    storageFolderInfo.MaximumSize = config.MaximumSize;
                    ConcludeStorageFolderInfoLoading(storageFolderInfo);
                }
                else
                {
                    // Not an already existing folder, let's create a new one
                    if (Directory.Exists(effectivePath))
                    {
                        var files = Directory.GetFiles(effectivePath);
                        if (files.Length > 0)
                        {
                            throw new ArgumentException($"{effectivePath} is a new StorageFolder but it already " +
                                $"contains files.", nameof(config));
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(effectivePath);
                    }

                    storageFolderInfo = new StorageFolderInfo();
                    storageFolderInfo.FullPath = effectivePath;
                    storageFolderInfo.UserPath = config.Path;
                    storageFolderInfo.MaximumSize = config.MaximumSize;
                    storageFolderInfo.NeedSaving = true;
                }
                m_StorageFolders[effectivePath] = storageFolderInfo;
            }
        }

        /// <summary>
        /// Updates configuration of a storage folder.
        /// </summary>
        /// <param name="config">New updated configuration of the storage folder.</param>
        /// <exception cref="ArgumentException">If asking to update a non existing storage folder or if the new
        /// configuration is not compatible.</exception>
        /// <remarks>This method was implemented to keep it as simple as possible.  In case where the new configuration
        /// has a lower maximum size than the current content of the storage folder we simply delete the oldest content,
        /// we don't try to migrate it to another storage folder (it will have to be re-downloaded from MissionControl
        /// if needed).</remarks>
        public void UpdateStorageFolder(StorageFolderConfig config)
        {
            lock (m_Lock)
            {
                string effectivePath = GetEffectiveStoragePath(config.Path);
                if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                {
                    throw new ArgumentException($"{nameof(FileBlobCache)} does not contain a StorageFolder with the " +
                        $"path {config.Path}.", nameof(config));
                }

                storageFolderInfo.MaximumSize = config.MaximumSize;
                storageFolderInfo.EvictsToFitInBudget(m_Logger);
            }
        }

        /// <summary>
        /// Persist the state of storage folders (that have changed and need to be saved).
        /// </summary>
        public void PersistStorageFolderStates()
        {
            lock (m_Lock)
            {
                foreach (var storageFolder in m_StorageFolders.Values)
                {
                    if (storageFolder.NeedSaving)
                    {
                        try
                        {
                            using FileStream serializeStream = File.Create(storageFolder.GetMetadataFilePath());
                            JsonSerializer.Serialize(serializeStream, storageFolder);
                            storageFolder.NeedSaving = false;
                        }
                        catch (Exception e)
                        {
                            m_Logger.LogWarning(e, "Failed to save state of {StorageFolderUserPath}",
                                storageFolder.UserPath);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Delete a storage folder (and all its content).
        /// </summary>
        /// <param name="path">Path of the storage folder to delete</param>
        /// <remarks>This method was implemented to keep it as simple as possible.  We simply delete all the cached
        /// content of the storage folder, we don't try to migrate it to another storage folder (it will have to be
        /// re-downloaded from MissionControl if needed).</remarks>
        public async Task DeleteStorageFolderAsync(string path)
        {
            string effectivePath = GetEffectiveStoragePath(path);
            for ( ; ; )
            {
                Task[]? taskOfFilesInUse;
                lock (m_Lock)
                {
                    if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                    {
                        throw new ArgumentException($"{nameof(FileBlobCache)} does not contain a StorageFolder with " +
                            $"the path {path}.", nameof(path));
                    }

                    storageFolderInfo.MaximumSize = 0;
                    storageFolderInfo.EvictsToFitInBudget(m_Logger);

                    if (storageFolderInfo.InUse.First == null)
                    {
                        // We deleted everything we could from the folder and nothing in it is still used, try to do
                        // the final deleted.
                        try
                        {
                            Directory.Delete(storageFolderInfo.FullPath, true);
                        }
                        catch(Exception e)
                        {
                            m_Logger.LogError(e, "Failed to completely cleanup {Path}", path);
                        }
                        m_StorageFolders.Remove(effectivePath);
                        return;
                    }

                    // If we reach this point, some files are still in use, wait until one of them is completed and try
                    // again.
                    taskOfFilesInUse = storageFolderInfo.InUse.Select(fi => Task.WhenAll(fi.AllTasks.ToArray())).ToArray();
                }
                await Task.WhenAny(taskOfFilesInUse);
            }
        }

        /// <summary>
        /// Remove a storage folder from the <see cref="FileBlobCache"/> without removing the files.
        /// </summary>
        /// <param name="path">Path of the storage folder to delete</param>
        /// <remarks>This method was implemented to keep it as simple as possible and will be delayed until there is
        /// no more file in use in the storage folder and new files might be fetch in that to be removed storage folder
        /// while we are waiting.  Not 100% optimal, but adding and removing storage folders shouldn't happen that often
        /// anyway.</remarks>
        public async Task RemoveStorageFolderAsync(string path)
        {
            StorageFolderInfo? disconnectedStorageFolder;

            string effectivePath = GetEffectiveStoragePath(path);
            for ( ; ; )
            {
                Task[]? taskOfFilesInUse;
                lock (m_Lock)
                {
                    if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                    {
                        throw new ArgumentException($"{nameof(FileBlobCache)} does not contain a StorageFolder with " +
                            $"the path {path}.", nameof(path));
                    }

                    if (storageFolderInfo.InUse.First == null)
                    {
                        // No files are in use, we can break the link between m_Files and the StorageFolderInfo.
                        foreach (var fileInfo in storageFolderInfo.InCache)
                        {
                            Debug.Assert(fileInfo.FetchTask == null);
                            Debug.Assert(fileInfo.CopyTasks.Count == 0);
                            Debug.Assert(fileInfo.StorageFolder == storageFolderInfo);
                            Debug.Assert(fileInfo.NodeInList != null);
                            Debug.Assert(storageFolderInfo.InCache.IsForThisList(fileInfo.NodeInList));
                            fileInfo.StorageFolder = null;
                            fileInfo.NodeInList = null;
                        }
                        foreach (var fileInfo in storageFolderInfo.Unreferenced)
                        {
                            Debug.Assert(fileInfo.FetchTask == null);
                            Debug.Assert(fileInfo.CopyTasks.Count == 0);
                            Debug.Assert(fileInfo.StorageFolder == storageFolderInfo);
                            Debug.Assert(fileInfo.NodeInList != null);
                            Debug.Assert(storageFolderInfo.Unreferenced.IsForThisList(fileInfo.NodeInList));
                            fileInfo.StorageFolder = null;
                            fileInfo.NodeInList = null;
                        }
                        m_StorageFolders.Remove(effectivePath);
                        disconnectedStorageFolder = storageFolderInfo;
                        break;
                    }

                    // If we reach this point, some files are still in use, wait until one of them is completed and try
                    // again.
                    taskOfFilesInUse = storageFolderInfo.InUse.Select(fi => Task.WhenAll(fi.AllTasks.ToArray())).ToArray();
                }
                await Task.WhenAny(taskOfFilesInUse);
            }

            // One last save before completely forgetting about the storage folder (in case someone wants to reload
            // it later on).
            if (disconnectedStorageFolder.NeedSaving)
            {
                try
                {
                    using FileStream serializeStream = File.Create(disconnectedStorageFolder.GetMetadataFilePath());
                    await JsonSerializer.SerializeAsync(serializeStream, disconnectedStorageFolder);
                }
                catch (Exception e)
                {
                    m_Logger.LogWarning(e, "Failed to save state of {DisconnectedFolderPath}",
                        disconnectedStorageFolder.UserPath);
                }
            }
        }

        /// <summary>
        /// Fetch (if necessary) and copy the given file to the given destination.
        /// </summary>
        /// <param name="fileBlobId">FileBlob (content) identifier.</param>
        /// <param name="toPath">Complete path (directory and filename) of where to copy the file.</param>
        /// <param name="cookie">Cookie passed to <see cref="FetchFileCallback"/> and <see cref="CopyFileCallback"/>.
        /// </param>
        /// <exception cref="ArgumentException">If no information about <paramref name="fileBlobId"/> can be found.
        /// </exception>
        /// <exception cref="InvalidOperationException">If no free space can be found to store the file in cache.
        /// </exception>
        public async Task CopyFileToAsync(Guid fileBlobId, string toPath, object? cookie = null)
        {
            Monitor.Enter(m_Lock);
            try
            {
                if (!m_Files.TryGetValue(fileBlobId, out var fileInfo) || fileInfo.ReferenceCount == 0)
                {
                    throw new ArgumentException($"Cannot find any information about fileBlobId of {fileBlobId}",
                        nameof(fileBlobId));
                }

                if (fileInfo.StorageFolder == null)
                {
                    // File is not stored anywhere, we must find where to store it.
                    var storageFolder = FindStorageFolderFor(fileInfo.CompressedSize);
                    if (storageFolder == null)
                    {
                        throw new InvalidOperationException("Failed to find free cache space to store the file " +
                            "before copy.");
                    }

                    // Add the FileInfo to that storage (as a file that is in use)
                    fileInfo.LastAccess = DateTime.Now;
                    fileInfo.StorageFolder = storageFolder;
                    fileInfo.NodeInList = storageFolder.InUse.AddLast(fileInfo);
                    storageFolder.NeedSaving = true;

                    // Fetch the file
                    var cachePath = storageFolder.GetPath(fileBlobId);
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    // Remarks: We might be tempted to do:
                    // var fetchTask = FetchFileCallback( ... )
                    // and skip the Task.Run(...) (to simplify and potentially optimize).
                    // However doing so would means that we execute FetchFileCallback from this thread, while m_Lock
                    // is locked.  So if FetchFileCallback does "too much processing" then the file blob cache would
                    // stay locked longer than it should.  We also cannot release the lock earlier as we need to update
                    // fileInfo.FetchTask.
                    var fetchTask = Task.Run(() => FetchFileCallback(fileBlobId, cachePath, cookie));
                    fileInfo.FetchTask = fetchTask;

                    Monitor.Exit(m_Lock);
                    Exception? fetchException = null;
                    try
                    {
                        await fetchTask;
                    }
                    catch (Exception e)
                    {
                        // Remember exception but don't throw it yet, we need to restore some states before
                        fetchException = e;
                    }
                    finally { Monitor.Enter(m_Lock); }
                    Debug.Assert(fileInfo.FetchTask == fetchTask);

                    fileInfo.FetchTask = null;
                    Debug.Assert(fileInfo.NodeInList != null);
                    Debug.Assert(storageFolder.InUse.IsForThisList(fileInfo.NodeInList));
                    storageFolder.InUse.Remove(fileInfo.NodeInList);

                    if (fetchException != null)
                    {
                        // There was a problem during the fetch, rollback and propagate the exception.
                        storageFolder.NeedSaving = true;
                        fileInfo.NodeInList = null;
                        fileInfo.StorageFolder = null;
                        ExceptionDispatchInfo.Capture(fetchException).Throw();
                    }
                    else
                    {
                        // The file is fetched, so it is now ready to be used -> InCache
                        fileInfo.NodeInList = storageFolder.InCache.AddLast(fileInfo);
                        storageFolder.NeedSaving = true;
                    }
                }
                else
                {
                    // Possible we want to copy what another thread started to fetch.  If that is the case then that
                    // other thread will have set fileInfo.FetchTask, so wait until it clears it (indicating the file
                    // is fully fetched and can be used to copy from).
                    while (fileInfo.FetchTask != null)
                    {
                        var waitTask = fileInfo.FetchTask;
                        if (waitTask.IsCompleted) // It is possible that we got m_Lock before the thread that initiated
                        {                         // the fetch.  If that it is the case wait a little bit and check again.
                            waitTask = Task.Delay(10);
                        }
                        Monitor.Exit(m_Lock);
                        try
                        {
                            await waitTask;
                        }
                        finally { Monitor.Enter(m_Lock); }
                    }
                }
                Debug.Assert(fileInfo.StorageFolder != null);

                // Prepare copy of the file
                fileInfo.LastAccess = DateTime.Now;
                if (fileInfo.CopyTasks.Count == 0)
                {
                    // First thread copying the file, move it to the in-use list.
                    Debug.Assert(fileInfo.NodeInList != null);
                    Debug.Assert(fileInfo.StorageFolder.InCache.IsForThisList(fileInfo.NodeInList));
                    fileInfo.StorageFolder.InCache.Remove(fileInfo.NodeInList);
                    fileInfo.NodeInList = fileInfo.StorageFolder.InUse.AddLast(fileInfo);
                    fileInfo.StorageFolder.NeedSaving = true;
                }
                // Remarks: We might be tempted to do:
                // var copyTask = CopyFileCallback( ... )
                // and skip the Task.Run(...) (to simplify and potentially optimize).
                // However doing so would means that we execute CopyFileCallback from this thread, while m_Lock is
                // locked.  So if CopyFileCallback does "too much processing" then the file blob cache would stay
                // locked longer than it should.  We also cannot release the lock earlier as we need to update
                // fileInfo.CopyTasks.
                var copyTask = Task.Run(() => CopyFileCallback(fileInfo.StorageFolder.GetPath(fileBlobId), toPath, cookie));
                fileInfo.CopyTasks.Add(copyTask);

                // Copy it
                Monitor.Exit(m_Lock);
                Exception? copyException = null;
                try
                {
                    await copyTask;
                }
                catch (Exception e)
                {
                    // Remember exception but don't throw it yet, we need to restore some states before
                    copyException = e;
                }
                finally { Monitor.Enter(m_Lock); }

                // Put the FileInfo back in the in-cache list if this is the last copy.
                bool removed = fileInfo.CopyTasks.Remove(copyTask);
                Debug.Assert(removed);
                if (fileInfo.CopyTasks.Count == 0)
                {
                    Debug.Assert(fileInfo.NodeInList != null);
                    Debug.Assert(fileInfo.StorageFolder.InUse.IsForThisList(fileInfo.NodeInList));
                    fileInfo.StorageFolder.InUse.Remove(fileInfo.NodeInList);
                    fileInfo.NodeInList = fileInfo.StorageFolder.InCache.AddLast(fileInfo);
                    fileInfo.StorageFolder.NeedSaving = true;
                }

                // Throw delayed exception
                if (copyException != null)
                {
                    ExceptionDispatchInfo.Capture(copyException).Throw();
                }
            }
            finally { Monitor.Exit(m_Lock); }
        }

        /// <summary>
        /// Computes the status of the storage folders in which the <see cref="FileBlobCache"/> stores cached files.
        /// </summary>
        /// <returns>The computed status.</returns>
        public StorageFolderStatus[] GetStorageFolderStatus()
        {
            lock (m_Lock)
            {
                StorageFolderStatus[] storageFolders = new StorageFolderStatus[m_StorageFolders.Count];
                int storageFolderIndex = 0;
                foreach (var info in m_StorageFolders.Values)
                {
                    var status = new StorageFolderStatus();
                    storageFolders[storageFolderIndex++] = status;

                    status.Path = info.UserPath;
                    status.CurrentSize = info.EffectiveSize;
                    status.UnreferencedSize = info.Unreferenced.CompressedSize;
                    status.ZombiesSize = info.ZombiesSize;
                    status.MaximumSize = info.MaximumSize;
                }
                return storageFolders;
            }
        }

        /// <summary>
        /// Return the final effective path (resolving relative path).
        /// </summary>
        /// <param name="path">User provided path.</param>
        public static string GetEffectiveStoragePath(string path)
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.GetFullPath(path, assemblyFolder!);
        }

        /// <summary>
        /// Conclude loading of a <see cref="StorageFolderInfo"/> (to be called after de-serialization).
        /// </summary>
        /// <param name="storageFolder">The freshly de-serialized <see cref="StorageFolderInfo"/>.</param>
        void ConcludeStorageFolderInfoLoading(StorageFolderInfo storageFolder)
        {
            // First, let's try to get rid of old zombies...
            var zombiesToKill = storageFolder.Zombies;
            storageFolder.Zombies = new();
            storageFolder.ZombiesSize = 0;
            foreach (var zombieId in zombiesToKill)
            {
                storageFolder.DeleteFile(m_Logger, zombieId);
            }

            // Now let's try to connect loaded FileInfo with the ones we already have in m_Files.  Since there is no 
            // guarantee reference and unreferenced files are the same then when saved, let's regroup them in a single
            // list and re-split them.
            FileInfoLinkedList toFindMatchFor = new();
            foreach (var fileInfo in storageFolder.Unreferenced)
            {
                toFindMatchFor.AddLast(fileInfo);
            }
            foreach (var fileInfo in storageFolder.InCache)
            {
                toFindMatchFor.AddLast(fileInfo);
            }
            foreach (var fileInfo in storageFolder.InUse)
            {
                toFindMatchFor.AddLast(fileInfo);
            }
            var (inCache, unreferenced) = FindMatchingFiles(storageFolder, toFindMatchFor);
            storageFolder.InCache = inCache;
            storageFolder.InUse.Clear();
            storageFolder.Unreferenced = unreferenced;

            // Maximum size might be smaller than it was, so we might need to delete some files...
            storageFolder.EvictsToFitInBudget(m_Logger);

            // Many things might have changed, so we need saving
            storageFolder.NeedSaving = true;
        }

        /// <summary>
        /// Search for matching <see cref="CacheFileInfo"/> in the global list of <see cref="CacheFileInfo"/> and fill
        /// a new list from them (and update the FileInfo to point in that new list).
        /// </summary>
        /// <param name="storageFolder"><see cref="StorageFolderInfo"/> from which all the files in
        /// <paramref name="fileInfoList"/> are from.</param>
        /// <param name="fileInfoList">List of <see cref="CacheFileInfo"/> to find in the global list.</param>
        /// <returns>The <see cref="FileInfoLinkedList"/> to which <see cref="CacheFileInfo"/> from
        /// <paramref name="fileInfoList"/> are now referencing through <see cref="CacheFileInfo.NodeInList"/>.</returns>
        (FileInfoLinkedList inCache, FileInfoLinkedList unreferenced) FindMatchingFiles(StorageFolderInfo storageFolder,
            FileInfoLinkedList fileInfoList)
        {
            // Split the files in two groups -> referenced and unreferenced.
            var inCache = new List<CacheFileInfo>();
            var unreferenced = new List<CacheFileInfo>();
            foreach (var fileInfo in fileInfoList)
            {
                if (m_Files.TryGetValue(fileInfo.Id, out var globalFileInfo))
                {
                    // We found an entry in m_Files, use this one instead of the one from fileInfoList
                    if (globalFileInfo.StorageFolder != null)
                    {
                        m_Logger.LogWarning("{FileInfoId} was found in both {StorageFolderPath1} and " +
                            "{StorageFolderPath2}, it will be deleted from {StorageFolderPathDelete}",
                            fileInfo.Id, storageFolder.UserPath, globalFileInfo.StorageFolder.UserPath,
                            storageFolder.UserPath);
                        storageFolder.DeleteFile(m_Logger, fileInfo.Id);
                        continue;
                    }

                    if (globalFileInfo.ReferenceCount > 0)
                    {
                        inCache.Add(globalFileInfo);
                    }
                    else
                    {
                        unreferenced.Add(globalFileInfo);
                    }
                    globalFileInfo.StorageFolder = storageFolder;
                }
                else
                {
                    // No entry found in m_Files, add one from this unreferenced file.
                    unreferenced.Add(fileInfo);
                    fileInfo.StorageFolder = storageFolder;
                    m_Files[fileInfo.Id] = fileInfo;
                }
            }

            // Sort each of them based on the last access time
            var sortedInCache = inCache.OrderBy(fi => fi.LastAccess);
            var sortedUnreferenced = unreferenced.OrderBy(fi => fi.LastAccess);

            // Created linked lists
            var inCacheLinked = new FileInfoLinkedList();
            foreach (var fileInfo in sortedInCache)
            {
                fileInfo.NodeInList = inCacheLinked.AddLast(fileInfo);
            }
            var unreferencedLinked = new FileInfoLinkedList();
            foreach (var fileInfo in sortedUnreferenced)
            {
                fileInfo.NodeInList = unreferencedLinked.AddLast(fileInfo);
            }
            return (inCacheLinked, unreferencedLinked);
        }

        /// <summary>
        /// Finds the StorageFolder where to store the given number of bytes.
        /// </summary>
        /// <param name="fileSize">Size of the file to store.</param>
        /// <returns>The <see cref="StorageFolderInfo"/>.</returns>
        /// <remarks>Will do some cleanup if there is no more free space or null if no folder with enough space can be
        /// found.</remarks>
        StorageFolderInfo? FindStorageFolderFor(long fileSize)
        {
            for ( ; ; )
            {
                // Sort storage folder based on their relative fullness and try to use the most empty one first.
                var sortedFolders = m_StorageFolders.Values.OrderBy(
                    sfi => (double)sfi.EffectiveSize / sfi.MaximumSize);
                foreach (var folder in sortedFolders)
                {
                    if (folder.FreeSpace >= fileSize)
                    {
                        return folder;
                    }
                }

                // Looks like all folders contains too much, find the one with the oldest cached file, evict it and
                // check again.
                var sortedUnreferenced = m_StorageFolders.Values.OrderBy(
                    sfi => sfi.Unreferenced.First == null ? DateTime.MaxValue : sfi.Unreferenced.First.Value.LastAccess);
                var oldestUnreferenced = sortedUnreferenced.First();
                if (oldestUnreferenced.Unreferenced.First != null)
                {
                    oldestUnreferenced.EvictNextUnreferenced(m_Logger);
                    continue;
                }
                var sortedCached = m_StorageFolders.Values.OrderBy(
                    sfi => sfi.InCache.First == null ? DateTime.MaxValue : sfi.InCache.First.Value.LastAccess);
                var oldestCached = sortedCached.First();
                if (oldestCached.InCache.First != null)
                {
                    oldestCached.EvictNextInCache(m_Logger);
                    continue;
                }

                // We still haven't been able to evict anything from any cache folder and we haven't found enough space
                // in a folder...  We could check to see if some files are being used, wait until they aren't anymore, ...
                // But anyway, things are looking really bad.  Either the requested file (or one being currently
                // processed) is too large or the cache too small.  So let's fail!
                return null;
            }
        }

        /// <summary>
        /// Object to use for logging
        /// </summary>
        ILogger m_Logger;

        /// <summary>
        /// Internal object used to lock access to member variables of this class
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// All the files managed by the <see cref="FileBlobCache"/>.
        /// </summary>
        /// <remarks>Including referenced files not yet present in any cache.</remarks>
        Dictionary<Guid, CacheFileInfo> m_Files = new();

        /// <summary>
        /// The folders in which we are storing files.
        /// </summary>
        Dictionary<string, StorageFolderInfo> m_StorageFolders = new();
    }
}
