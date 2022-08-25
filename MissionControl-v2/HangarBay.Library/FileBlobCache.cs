using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Main class responsible for managing the files in the different storage folders.
    /// </summary>
    public class FileBlobCache
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
        /// Function called by <see cref="FileBlobCache"/> when asked to copy a file.
        /// </summary>
        /// <remarks>Func first string is the path to the file to copy and the second one is the path to the
        /// destination.  Returns a <see cref="Task"/> that is to be completed when copy is finished.</remarks>
        public Func<string, string, Task> CopyFileCallback { get; set; } = (string _, string _) => Task.CompletedTask;

        /// <summary>
        /// Function called by <see cref="FileBlobCache"/> when a file is to be fetched.
        /// </summary>
        /// <remarks>Func Guid is the fileblob identifier of the file to fetch and the string is the path of where to
        /// save that fetched content.  Returns a <see cref="Task"/> that is to be completed when fetch is completed.
        /// </remarks>
        public Func<Guid, string, Task> FetchFileCallback { get; set; } = (Guid _, string _) => Task.CompletedTask;

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
                        throw new ArgumentException(nameof(compressedSize), "CompressedSize does not match compressed size of previous entry.  A given fileBlobId should always have the same compressed size.");
                    }
                    if (size != fileInfo.Size)
                    {
                        throw new ArgumentException(nameof(size), "Size does not match size of previous entry.  A given fileBlobId should always have the same compressed size.");
                    }
                    ++fileInfo.ReferenceCount;

                    if (fileInfo.ReferenceCount == 1 && fileInfo.StorageFolder != null)
                    {
                        // We are starting to use (for the first time) a file that is already present in a storage
                        // folder (was probably used by another payload that has been removed).  Move it from the
                        // unreferenced list to the InCache list.  Putting it as the last one to be evicted while we are
                        // still not 100% sure it will be used might not be perfect but it is much simpler (so it let's
                        // keep it simple for now and we will change that if ever we see it causes problems...).
                        Debug.Assert(fileInfo.NodeInList != null);
                        Debug.Assert(fileInfo.StorageFolder.Unreferenced.IsForThisList(fileInfo.NodeInList));
                        fileInfo.StorageFolder.Unreferenced.Remove(fileInfo.NodeInList);
                        fileInfo.NodeInList = fileInfo.StorageFolder.InCache.AddLast(fileInfo);

                        // List of files in the StorageFolder has changed, so it needs to be saved.
                        fileInfo.StorageFolder.NeedSaving = true;
                    }
                }
                else
                {
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
                    m_Logger.LogWarning($"Trying to remove a reference to {fileBlobId} but there is no file with that " +
                        $"identifier.  Will skip but this is not supposed to happen.");
                    return;
                }
                if (fileInfo.ReferenceCount <= 0)
                {
                    m_Logger.LogWarning($"Trying to remove a reference to {fileBlobId} but the file was not already " +
                        $"referenced.  Will skip but this is not supposed to happen.");
                    return;
                }
                if (fileInfo.ReferenceCount == 1 && (fileInfo.FetchTask != null || fileInfo.CopyTasks.Count > 0))
                {
                    if (!muteDefer)
                    {
                        m_Logger.LogWarning($"Unexpected, trying to remove the last usage of {fileBlobId} that is " +
                            $"currently in use, will defer its processing (asynchronously) for when it will not be " +
                            $"used anymore.");
                    }
                    Task.WhenAll(fileInfo.AllTasks).ContinueWith(t => DecreaseUsageCount(fileBlobId, true));
                    return;
                }
                --fileInfo.ReferenceCount;
                if (fileInfo.ReferenceCount == 0)
                {
                    if (fileInfo.StorageFolder != null)
                    {
                        // Last reference of a file a folder, transfer it to the unreferenced file list.
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
        /// <remarks>Payloads potentially referencing those files need to be added before the folder or otherwise the
        /// last used time of the file will be cleared (since the file will not be referenced).</remarks>
        /// <exception cref="ArgumentException">If asking to add a storage folder we already have or this is a new
        /// storage folder and it is not empty.</exception>
        public void AddStorageFolder(StorageFolderConfig config)
        {
            lock (m_Lock)
            {
                string effectivePath = GetEffectiveStoragePath(config.Path);
                if (m_StorageFolders.ContainsKey(effectivePath))
                {
                    throw new ArgumentException(nameof(config),
                        $"{nameof(FileBlobCache)} already contain a StorageFolder with the path {effectivePath}.");
                }

                // Load information about the folder
                StorageFolderInfo storageFolderInfo;
                string storageFolderMetadataJson = Path.Combine(effectivePath, "metadata.json");
                if (File.Exists(storageFolderMetadataJson))
                {
                    StorageFolderInfo? deserialized;
                    using (var createStream = File.Open(storageFolderMetadataJson, FileMode.Open))
                    {
                        deserialized = JsonSerializer.Deserialize<StorageFolderInfo>(createStream);
                    }
                    if (deserialized == null)
                    {
                        throw new NullReferenceException(
                            $"Got an unexpected null StorageFolderInfo while de-serializing {storageFolderMetadataJson}.");
                    }
                    storageFolderInfo = deserialized;
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
                            throw new ArgumentException(nameof(config),
                                $"{effectivePath} is a new StorageFolder but it already contains files.");
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
        /// we don't try to migrated it to another storage folder (it will have to be re-downloaded from MissionControl
        /// if needed).</remarks>
        public void UpdateStorageFolder(StorageFolderConfig config)
        {
            lock (m_Lock)
            {
                string effectivePath = GetEffectiveStoragePath(config.Path);
                if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                {
                    throw new ArgumentException(nameof(config),
                        $"{nameof(FileBlobCache)} does not contain a StorageFolder with the path {config.Path}.");
                }

                storageFolderInfo.MaximumSize = config.MaximumSize;
                storageFolderInfo.EvictsToFitInBudget(m_Logger);
            }
        }

        /// <summary>
        /// Persist the state of storage folders (that need updating).
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
                            string storageFolderMetadataJson = Path.Combine(storageFolder.FullPath, "metadata.json");
                            using FileStream createStream = File.Create(storageFolderMetadataJson);
                            JsonSerializer.Serialize(createStream, storageFolder);
                            storageFolder.NeedSaving = false;
                        }
                        catch (Exception e)
                        {
                            m_Logger.LogWarning($"Failed to save state of {storageFolder.UserPath}: {e}");
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
        /// content or the storage folder, we don't try to migrated it to another storage folder (it will have to be
        /// re-downloaded from MissionControl if needed).</remarks>
        public async Task DeleteStorageFolderAsync(string path)
        {
            string effectivePath = GetEffectiveStoragePath(path);
            for ( ; ; )
            {
                Task[]? taskOfFilesInUse = null;
                lock (m_Lock)
                {
                    if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                    {
                        throw new ArgumentException(nameof(path),
                            $"{nameof(FileBlobCache)} does not contain a StorageFolder with the path {path}.");
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
                            m_Logger.LogError($"Failed to completely cleanup {path}: {e}");
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
        /// Fetch (if necessary) and copy the given file to the given destination.
        /// </summary>
        /// <param name="fileBlobId">FileBlob (content) identifier.</param>
        /// <param name="toPath">Complete path (directory and filename) of where to copy the file.</param>
        /// <exception cref="ArgumentException">If no information about <paramref name="fileBlobId"/> can be found.
        /// </exception>
        /// <exception cref="InvalidOperationException">If no free space can be found to store the file in cache.
        /// </exception>
        public async Task CopyFileToAsync(Guid fileBlobId, string toPath)
        {
            Monitor.Enter(m_Lock);
            try
            {
                if (!m_Files.TryGetValue(fileBlobId, out var fileInfo) || fileInfo.ReferenceCount == 0)
                {
                    throw new ArgumentException(nameof(fileBlobId),
                        $"Cannot find any information about fileBlobId of {fileBlobId}");
                }

                if (fileInfo.StorageFolder == null)
                {
                    // File is not stored anywhere, we must find where to store it.
                    var storageFolder = FindStorageFolderFor(fileInfo.CompressedSize);
                    if (storageFolder == null)
                    {
                        throw new InvalidOperationException("Failed to find free cache space to store the file before copy.");
                    }

                    // Add the FileInfo to that storage (as a file that is in use)
                    fileInfo.LastAccess = DateTime.Now;
                    fileInfo.StorageFolder = storageFolder;
                    fileInfo.NodeInList = storageFolder.InUse.AddLast(fileInfo);
                    storageFolder.NeedSaving = true;

                    // Fetch the file
                    var fetchTask = Task.Run(async () =>
                        {
                            var cachePath = storageFolder.GetPath(fileBlobId);
                            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                            await FetchFileCallback(fileBlobId, cachePath);
                        }
                    );
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
                        throw fetchException;
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
                    // Possible we want to copy what another thread started to fetch, wait until it is done fetching
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
                var copyTask = Task.Run(async () => await CopyFileCallback(fileInfo.StorageFolder.GetPath(fileBlobId), toPath));
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
                    throw copyException;
                }
            }
            finally { Monitor.Exit(m_Lock); }
        }

        /// <summary>
        /// Computes the status of the storage folders in which the <see cref="FileBlobCache"/> stores cached files.
        /// </summary>
        /// <returns></returns>
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
        /// Return the final effective path (resolving relative path, case insensitivity under windows, ...)
        /// </summary>
        /// <param name="path">User provided path.</param>
        string GetEffectiveStoragePath(string path)
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            path = Path.GetFullPath(path, assemblyFolder!);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = path.ToLower();
            }
            return path;
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

            // Now let's try to connect loaded FileInfo with the ones we already have in m_Files.  Since there is
            // guarantee reference and unreferenced files are the same let's regroup them in a single list and re-split
            // them.
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
            (var inCache, var unreferenced) = FindMatchingFiles(storageFolder, toFindMatchFor);
            storageFolder.InCache = inCache;
            storageFolder.InUse.Clear();
            storageFolder.Unreferenced = unreferenced;

            // Maximum size might be smaller than it was, so we might need to delete some files...
            storageFolder.EvictsToFitInBudget(m_Logger);

            // Many things might have changed, so we need saving
            storageFolder.NeedSaving = true;
        }

        /// <summary>
        /// Search for matching <see cref="CacheFileInfo"/> in the global list of <see cref="CacheFileInfo"/> and fill a new list
        /// from them (and update the FileInfo to point in that new list).
        /// </summary>
        /// <param name="storageFolder"><see cref="StorageFolderInfo"/> from which all the files in
        /// <paramref name="fileInfoList"/> are from.</param>
        /// <param name="fileInfoList">List of <see cref="CacheFileInfo"/> to find in the global list.</param>
        /// <returns>The <see cref="CacheFileInfo"/> list to which entries in the global list are now pointing to.</returns>
        (FileInfoLinkedList, FileInfoLinkedList) FindMatchingFiles(StorageFolderInfo storageFolder, FileInfoLinkedList fileInfoList)
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
                        m_Logger.LogWarning($"{fileInfo.Id} was found in both {storageFolder.UserPath} and " +
                            $"{globalFileInfo.StorageFolder.UserPath}, it will be deleted from {storageFolder.UserPath}.");
                        storageFolder.DeleteFile(m_Logger, fileInfo.Id);
                        continue;
                    }

                    // Assert below is because if it would be == 0, it means that it has have an entry in a storage
                    // folder, so the if above should have generated a warning, deleted the duplicated file and moved
                    // to the next entry.
                    Debug.Assert(globalFileInfo.ReferenceCount > 0);
                    inCache.Add(globalFileInfo);
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
        /// <param name="fileSize"></param>
        /// <returns>The <see cref="StorageFolderInfo"/>.</returns>
        /// <remarks>Will do some cleanup if there is no more free space or null if no folder with enough space can be
        /// found.</remarks>
        StorageFolderInfo? FindStorageFolderFor(long fileSize)
        {
            for ( ; ; )
            {
                // Sort storage folder based on their relative fullness and try to use the most empty one.
                var sortedFolders = m_StorageFolders.Values.OrderBy(
                    sfi => (double)sfi.EffectiveSize / (double)sfi.MaximumSize);
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
                    oldestUnreferenced.EvictNextUnrefrenced(m_Logger);
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
