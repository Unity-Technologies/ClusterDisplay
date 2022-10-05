using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Exception thrown when no storage folder contains enough free space for the file to add.
    /// </summary>
    public class StorageFolderFullException: InvalidOperationException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public StorageFolderFullException() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Exception message.</param>
        public StorageFolderFullException(string message) : base (message) { }
    }

    /// <summary>
    /// Class responsible for managing all the file blobs of a MissionControl process.
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global -> Used by mocking in unit tests
    public class FileBlobsManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        public FileBlobsManager(ILogger logger)
        {
            m_Logger = logger;
        }

        /// <summary>
        /// Adds a storage folder to the file blob manager (and add any files it would contains to the "in-memory"
        /// indexes).
        /// </summary>
        /// <param name="config">Information about the storage folder.</param>
        /// <exception cref="ArgumentException">If asking to add a storage folder we already have or this is a new
        /// storage folder and it is not empty.</exception>
        public void AddStorageFolder(StorageFolderConfig config)
        {
            lock (m_Lock)
            {
                string effectivePath = GetEffectiveStoragePath(config.Path);
                if (m_StorageFolders.ContainsKey(effectivePath))
                {
                    throw new ArgumentException($"{nameof(FileBlobsManager)} already contain a StorageFolder with " +
                        $"the path {effectivePath}.", nameof(config));
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
                        throw new ArgumentException($"{nameof(FileBlobsManager)} already contain a StorageFolder " +
                            $"with the path {pathToTheSameFolder} that is the equivalent to {config.Path}.",
                            nameof(config));
                    }
                }

                // Load information about the folder
                StorageFolderInfo storageFolderInfo;
                string storageFolderMetadataJson = StorageFolderInfo.GetMetadataFilePath(effectivePath);
                if (File.Exists(storageFolderMetadataJson))
                {
                    StorageFolderInfo? deserialized;
                    using (var loadStream = File.OpenRead(storageFolderMetadataJson))
                    {
                        deserialized = JsonSerializer.Deserialize<StorageFolderInfo>(loadStream, Json.SerializerOptions);
                    }

                    storageFolderInfo = deserialized ?? throw new NullReferenceException(
                        $"Got an unexpected null StorageFolderInfo while de-serializing {storageFolderMetadataJson}.");
                    storageFolderInfo.UserPath = config.Path;
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
                    storageFolderInfo.UserPath = config.Path;
                    storageFolderInfo.FullPath = effectivePath;
                    storageFolderInfo.MaximumSize = config.MaximumSize;
                }
                m_StorageFolders[effectivePath] = storageFolderInfo;
            }
        }

        /// <summary>
        /// Remove a storage folder from the <see cref="FileBlobsManager"/> moving the files to other storage folders.
        /// </summary>
        /// <param name="path">Path of the storage folder to remove.</param>
        /// <exception cref="StorageFolderFullException">Not enough free space in the other storage folder for the
        /// content of the storage folder we have been asked to remove.</exception>
        /// <remarks>Implementation of this method could probably be faster, however calling this method should be
        /// really rare and only in case of a big system maintenance (removing a disk or something similar).  So the
        /// implementation targets a fair performance with a easier to maintain code (vs the faster performance
        /// possible).</remarks>
        public async Task RemoveStorageFolderAsync(string path)
        {
            using var _ = await m_MovingFilesLock.LockAsync();

            string effectivePath = GetEffectiveStoragePath(path);
            try
            {
                for (; ; )
                {
                    // Get the list of files to move
                    List<FileBlobInfo> filesToMove = new();
                    lock (m_Lock)
                    {
                        if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                        {
                            throw new ArgumentException($"{nameof(FileBlobsManager)} does not contain a StorageFolder " +
                                $"with the path {path}.", nameof(path));
                        }

                        long blobsSize = storageFolderInfo.Size - storageFolderInfo.ZombiesSize;
                        long otherStorageFreeSpace =
                            m_StorageFolders.Values.Where(sf => sf != storageFolderInfo).Sum(sf => sf.FreeSpace);
                        if (blobsSize > otherStorageFreeSpace)
                        {
                            throw new StorageFolderFullException(
                                $"Remaining storage folders do not have enough space ({otherStorageFreeSpace} bytes) " +
                                $"to store the files of the remove storage folder ({blobsSize} bytes).");
                        }

                        filesToMove.AddRange(storageFolderInfo.Files);

                        if (filesToMove.Count == 0 && m_PendingMoves.Count == 0)
                        {
                            // Storage folder does not have any content anymore, we can remove it!
                            m_StorageFolders.Remove(effectivePath);
                            return;
                        }
                    }

                    // Move them
                    await TryToMoveFilesAsync(filesToMove);
                }
            }
            finally
            {
                lock (m_Lock)
                {
                    AbortPendingFileMove();
                }
            }
        }

        /// <summary>
        /// Updates configuration of a storage folder.
        /// </summary>
        /// <param name="config">New updated configuration of the storage folder.</param>
        /// <exception cref="ArgumentException">If asking to update a non existing storage folder or if the new
        /// configuration is not compatible.</exception>
        /// <remarks>Implementation of this method could probably be faster, however calling this method should be
        /// really rare and only in case of a big system maintenance (add or removing a disk or something similar).  So
        /// the implementation targets a fair performance with a easier to maintain code (vs the faster performance
        /// possible).</remarks>
        public async Task UpdateStorageFolderAsync(StorageFolderConfig config)
        {
            using var _ = await m_MovingFilesLock.LockAsync();

            string effectivePath = GetEffectiveStoragePath(config.Path);
            try
            {
                for (; ; )
                {
                    // Get the list of files to move so that the update storage folder is not "oversize"
                    List<FileBlobInfo> filesToMove = new();
                    lock (m_Lock)
                    {
                        if (!m_StorageFolders.TryGetValue(effectivePath, out var storageFolderInfo))
                        {
                            throw new ArgumentException($"{nameof(FileBlobsManager)} does not contain a StorageFolder " +
                                $"with the path {config.Path}.", nameof(config));
                        }

                        // Find how much data need to be moved considering the new maximum size of the folder
                        long toFree = storageFolderInfo.Size - config.MaximumSize;
                        long otherStorageFreeSpace =
                            m_StorageFolders.Values.Where(sf => sf != storageFolderInfo).Sum(sf => sf.FreeSpace);
                        if (toFree > otherStorageFreeSpace)
                        {
                            throw new StorageFolderFullException(
                                $"Remaining storage folders do not have enough space ({otherStorageFreeSpace} bytes) " +
                                $"to store the files that have to be evicted from {config.Path}.");
                        }

                        long addedToList = 0;
                        foreach (var fileOfFolder in storageFolderInfo.Files)
                        {
                            if (addedToList >= toFree)
                            {
                                break;
                            }
                            filesToMove.Add(fileOfFolder);
                            addedToList += fileOfFolder.CompressedSize;
                        }

                        if (filesToMove.Count == 0 && m_PendingMoves.Count == 0)
                        {
                            if (storageFolderInfo.Size > config.MaximumSize)
                            {
                                throw new InvalidOperationException($"Cannot find enough content to move out " +
                                    $"{config.Path} to free enough space to respect the new maximum size of " +
                                    $"{config.MaximumSize} bytes.");
                            }

                            // Nothing to move anymore, we can perform the update
                            storageFolderInfo.MaximumSize = config.MaximumSize;
                            return;
                        }
                    }

                    // Move them
                    await TryToMoveFilesAsync(filesToMove);
                }
            }
            finally
            {
                lock (m_Lock)
                {
                    AbortPendingFileMove();
                }
            }
        }

        /// <summary>
        /// Add a file blob to the collection of file blobs (or re-use duplicates if one can be found).
        /// </summary>
        /// <param name="fileContent">File content.</param>
        /// <param name="fileLength">Length of the complete file provided by <paramref name="fileContent"/>.</param>
        /// <param name="md5">MD5 checksum of <paramref name="fileContent"/>.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Identifier of the new or reused file blob.</returns>
        /// <remarks>We are asking for the file length in a dedicated parameter (<paramref name="fileLength"/>) as
        /// opposed of using <see cref="Stream.Length"/> since some Stream implementations does not support it.
        /// <br/><br/>
        /// ReferenceCount of the <see cref="FileBlobInfo"/> corresponding to the returned identifier is increased by
        /// one and will have to eventually be decreased using <see cref="DecreaseFileBlobReferenceAsync"/>.
        /// </remarks>
        /// <exception cref="StorageFolderFullException">If no storage folder is large enough to store the uncompressed
        /// file.</exception>
        public virtual async Task<Guid> AddFileBlobAsync(Stream fileContent, long fileLength, Guid md5,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource fillFileTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            IEnumerable<FileBlobInfo> withSameMd5 = Enumerable.Empty<FileBlobInfo>();
            FileBlobInfo? newFileBlobInfo = null;
            FileBlobInfo? fileBlobInfoWithContent = null;

            try
            {
                // Prepare adding the file blob
                for (; ; )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    List<Task> toWaitOn = new();
                    lock (m_Lock)
                    {
                        // Let's get all the already existing files with the same MD5.  Should normally be 0 or 1, or
                        // really low...
                        withSameMd5 = GetFileBlobsWithMd5(md5);

                        // Is any of them still being prepared?  We cannot continue if that is the case and will have to
                        // wait for it to be completed before continuing (again, this should be super rare).
                        foreach (var fileInfo in withSameMd5)
                        {
                            if (!fileInfo.IsReady)
                            {
                                toWaitOn.InsertRange(toWaitOn.Count, fileInfo.Using);
                            }
                        }

                        // Add the file to a storage folder while we are filling it.
                        // Remarks: We do not yet know the compressed file size, so assume no compression to be sure we do
                        // not put too much content in the folder.
                        if (toWaitOn.Count == 0)
                        {
                            var storageFolder = GetStorageFolderFor(fileLength);
                            if (storageFolder == null)
                            {
                                throw new StorageFolderFullException(
                                    $"No storage folder with enough free space ({fileLength} bytes).");
                            }

                            newFileBlobInfo = new(Guid.NewGuid(), md5, fileLength, fileLength);
                            newFileBlobInfo.StorageFolder = storageFolder;
                            storageFolder.AddFile(newFileBlobInfo);
                            newFileBlobInfo.Using.Add(fillFileTaskCompletionSource.Task);
                            m_IdDictionary.Add(newFileBlobInfo.Id, newFileBlobInfo);
                            SetFileBlobsWithMd5(md5, withSameMd5­.Append(newFileBlobInfo));

                            // We will be using file blobs in withSameMd5 to try to find duplicates, so register a task to
                            // signal when we are done of them.
                            foreach (var fileInfo in withSameMd5)
                            {
                                fileInfo.Using.Add(fillFileTaskCompletionSource.Task);
                            }

                            // Ready to start compressing
                            break;
                        }
                    }

                    if (toWaitOn.Count > 0)
                    {
                        await Task.WhenAll(toWaitOn);
                    }
                }

                // Add the file to the storage folder (and search for duplicates while at it)
                fileBlobInfoWithContent = await CompressFileAsync(fileContent, fileLength, newFileBlobInfo, withSameMd5,
                    md5, cancellationToken);

                // We are almost done, but we know the value we want to return, concluding everything will be done in
                // the finally.
                return fileBlobInfoWithContent.Id;
            }
            finally
            {
                lock (m_Lock)
                {
                    foreach (var fileInfo in withSameMd5)
                    {
                        fileInfo.Using.Remove(fillFileTaskCompletionSource.Task);
                    }

                    if (newFileBlobInfo != null)
                    {
                        Debug.Assert(newFileBlobInfo.StorageFolder != null);
                        newFileBlobInfo.StorageFolder.RemoveFile(newFileBlobInfo);
                        m_IdDictionary.Remove(newFileBlobInfo.Id);

                        // Since AddFileBlobAsync waits for not ready FileBlobInfo with the same md5, and
                        // newFileBlobInfo is not ready, no one else should have added a new FileBlobInfo with that
                        // md5.  Similarly no one should have removed a FileBlobInfo with that md5 since we added a
                        // task that is using them.  So the list of FileBlobInfo with that fileBlobInfo shouldn't have
                        // changed.
                        Debug.Assert(GetFileBlobsWithMd5(md5).SequenceEqual(withSameMd5.Append(newFileBlobInfo)));

                        if (newFileBlobInfo == fileBlobInfoWithContent)
                        {
                            // No equivalent found, add the new real compressed FileBlobInfo
                            var compressedFileInfo = new FileInfo(newFileBlobInfo.Path);
                            FileBlobInfo finalNewBlobInfo = new (newFileBlobInfo.Id, md5, compressedFileInfo.Length,
                                fileLength);
                            finalNewBlobInfo.StorageFolder = newFileBlobInfo.StorageFolder;
                            finalNewBlobInfo.StorageFolder.AddFile(finalNewBlobInfo);
                            finalNewBlobInfo.ReferenceCount = 1;
                            finalNewBlobInfo.IsReady = true;
                            m_IdDictionary.Add(finalNewBlobInfo.Id, finalNewBlobInfo);
                            withSameMd5­ = withSameMd5­.Append(finalNewBlobInfo);
                        }
                        else if (fileBlobInfoWithContent != null)
                        {
                            ++fileBlobInfoWithContent.ReferenceCount;
                        }
                        // else, fileBlobInfoWithContent is an already existing FileBlobInfo, nothing to change (just
                        // have to update FileBlobInfo with the md5 of the new content to remove the temporary one we
                        // created).
                        SetFileBlobsWithMd5(md5, withSameMd5­);
                    }

                    fillFileTaskCompletionSource.SetResult();

                    foreach (var fileInfo in withSameMd5)
                    {
                        CheckForConcludeFileBlobMove(fileInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Increase reference count of a file already present in a storage folder.
        /// </summary>
        /// <param name="id">Identifier of the file blob.</param>
        /// <exception cref="KeyNotFoundException">If asking for an unknown <paramref name="id"/>.</exception>
        /// <remarks>This method is especially useful when loading the system and restoring references of previous
        /// payloads to the file blobs they are using.</remarks>
        public virtual void IncreaseFileBlobReference(Guid id)
        {
            lock (m_Lock)
            {
                if (!m_IdDictionary.TryGetValue(id, out var fileBlobInfo))
                {
                    throw new KeyNotFoundException($"Cannot find any file blob wit the identifier {id}");
                }
                ++fileBlobInfo.ReferenceCount;
            }
        }

        /// <summary>
        /// Unreferenced a file added through <see cref="AddFileBlobAsync"/> or that had its reference count increased
        /// using <see cref="IncreaseFileBlobReference"/>.
        /// </summary>
        /// <param name="id">Identifier of the file blob.</param>
        /// <remarks>Will delete the file in the storage if there reference count of the file reaches 0.</remarks>
        /// <exception cref="KeyNotFoundException">If asking for an unknown <paramref name="id"/>.</exception>
        /// <exception cref="InvalidOperationException">If asking to decrease reference to a non referenced blob.
        /// </exception>
        public virtual async Task DecreaseFileBlobReferenceAsync(Guid id)
        {
            for (; ; )
            {
                Task waitTask;
                lock (m_Lock)
                {
                    if (!m_IdDictionary.TryGetValue(id, out var toRemoveInfo))
                    {
                        throw new KeyNotFoundException($"Cannot find any file blob wit the identifier {id}");
                    }
                    if (toRemoveInfo.ReferenceCount == 0)
                    {
                        // It is in theory possible to have file blobs with a reference count of 0 (loading back a
                        // storage folder that contains a file but no payload reference it afterwards).  Regardless,
                        // caller should only decrease reference of something that it previously referenced.
                        throw new InvalidOperationException($"Cannot decrease reference count of an unreferenced file " +
                            $"blob ({id})");
                    }

                    if (toRemoveInfo.ReferenceCount == 1)
                    {
                        if (toRemoveInfo.Using.Count == 0)
                        {
                            // We are removing the last reference and no one is using the files blob, we can proceed.
                            Debug.Assert(toRemoveInfo.StorageFolder != null);

                            toRemoveInfo.StorageFolder.RemoveFile(toRemoveInfo);
                            RemoveFileBlobInfoFromMd5Dictionary(toRemoveInfo);
                            m_IdDictionary.Remove(id);

                            DeleteFileOfBlobInfo(toRemoveInfo);
                            return;
                        }
                        else
                        {
                            // Some people are using the file, we cannot remove the last reference yet...
                            waitTask = Task.WhenAll(toRemoveInfo.Using);
                        }
                    }
                    else
                    {
                        // There are still other references, we can simply decrease the reference count without any
                        // special work.
                        --toRemoveInfo.ReferenceCount;
                        return;
                    }
                }

                // If does ot have to wait, everything should be done and we should have returned
                Debug.Assert(waitTask != null);
                await waitTask;
            }
        }

        /// <summary>
        /// Locks the file blob with the given identifier so that it cannot be moved or deleted.
        /// </summary>
        /// <param name="id">File blob identifier.</param>
        /// <returns><see cref="FileBlobLock"/> keeping the file blob locked until disposed of.</returns>
        /// <exception cref="KeyNotFoundException">If asking for an unknown <paramref name="id"/>.</exception>
        public virtual async Task<FileBlobLock> LockFileBlob(Guid id)
        {
            for (; ; )
            {
                Task toWaitOn;
                lock (m_Lock)
                {
                    // Might throw if missing, but it will throw a KeyNotFoundException which is exactly what we want.
                    var fileBlobInfo = m_IdDictionary[id];

                    // Return the new LockedFileBlob (if ready)
                    if (fileBlobInfo.IsReady)
                    {
                        return CreateFileBlobLock(fileBlobInfo);
                    }

                    // If we reach this point file blob is not yet ready (how was anyone able to find its id through a
                    // "normal use case" is a little bit beyond me but its easy to support, so let's support it.
                    Debug.Assert(!fileBlobInfo.IsReady);
                    Debug.Assert(fileBlobInfo.Using.Any());
                    toWaitOn = Task.WhenAll(fileBlobInfo.Using);
                }

                // If does ot have to wait, everything should be done and we should have returned
                Debug.Assert(toWaitOn != null);
                await toWaitOn;
            }
        }

        /// <summary>
        /// Computes the status of the storage folders in which the <see cref="FileBlobsManager"/> stores cached files.
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
                    status.CurrentSize = info.Size;
                    status.ZombiesSize = info.ZombiesSize;
                    status.MaximumSize = info.MaximumSize;
                }
                return storageFolders;
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
                    // Save the storage folder if it needs to be saved and all its files are ready.
                    if (storageFolder.NeedSaving)
                    {
                        try
                        {
                            using FileStream serializeStream = File.Create(storageFolder.GetMetadataFilePath());
                            JsonSerializer.Serialize(serializeStream, storageFolder, Json.SerializerOptions);
                            storageFolder.Saved();
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
            // Update our id and md5 indexes.
            Guid lastReIndexedFileBlobId = Guid.Empty;
            List<FileBlobInfo> filesToRemove = new();
            try
            {
                foreach (var file in storageFolder.Files)
                {
                    if (!file.IsReady)
                    {
                        // Ok, we have a non ready file.  We could be tempted to simply throw it away, but it might be
                        // a perfectly good file and we simply were not able to save the storage folder metadata after
                        // completing it (process killed?).  So check if the fill look good and take the decision based
                        // on that.
                        if (IsNonReadyFileGood(file))
                        {
                            m_Logger.LogWarning("File blob {Id} was tagged as not ready but it looks complete, so we " +
                                "will change its ready status so that it can be used", file.Id);
                            file.IsReady = true;
                        }
                        else
                        {
                            m_Logger.LogError("File blob {Id} was tagger as not ready and does not look like it is " +
                                "complete, it will be deleted", file.Id);
                            filesToRemove.Add(file);
                        }
                    }

                    if (file.IsReady)
                    {
                        lastReIndexedFileBlobId = file.Id;
                        m_IdDictionary.Add(file.Id, file);
                        if (m_Md5Dictionary.TryGetValue(file.Md5, out var fileBlobWithSameMd5))
                        {
                            file.NextInSameMd5Chain = fileBlobWithSameMd5;
                        }
                        m_Md5Dictionary[file.Md5] = file;
                    }
                }
            }
            catch (ArgumentException e)
            {
                m_Logger.LogError(e, "Conflict found while indexing files of {Path}, another folder already contains " +
                    "file blob {Id}", storageFolder.FullPath, lastReIndexedFileBlobId);
                throw;
            }

            // Discard some files we decided to not keep
            foreach (var toDelete in filesToRemove)
            {
                storageFolder.RemoveFile(toDelete);
            }

            // Search for zombies (files present on disk and not in storageFolder.Files) and kill them!
            var files = Directory.GetFiles(storageFolder.FullPath, "*", new EnumerationOptions() {RecurseSubdirectories = true});
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                bool deleteFile = false;
                if (Guid.TryParse(filename, out var id))
                {
                    if (!storageFolder.ContainsFileBlob(id))
                    {
                        deleteFile = true;
                    }
                }
                else if (filename != Path.GetFileName(storageFolder.GetMetadataFilePath()))
                {
                    deleteFile = true;
                }

                if (deleteFile)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch(Exception e)
                    {
                        m_Logger.LogError(e, "Failed to delete {File}", file);
                        try
                        {
                            FileInfo zombieInfo = new(file);
                            storageFolder.CountZombies(zombieInfo.Length);
                        }
                        catch (Exception e2)
                        {
                            m_Logger.LogError(e2, "Failed to get information about {File}, StorageFolder size might " +
                                "be wrong", file);
                        }
                    }
                }
            }

            // Less entertaining but still good to do, let's search for empty sub-folder and delete them.
            CleanSubFolders(storageFolder.FullPath);
        }

        /// <summary>
        /// Check if the file of a <see cref="FileBlobInfo"/> appear to be complete or not.
        /// </summary>
        /// <param name="fileBlob">The <see cref="FileBlobInfo"/>.</param>
        /// <remarks>This is a blocking call (not async).  Not ideal, but it should only be called in case something
        /// look bad at load time, so it shouldn't be too bad.</remarks>
        static bool IsNonReadyFileGood(FileBlobInfo fileBlob)
        {
            try
            {
                using var compressedStream = File.OpenRead(fileBlob.Path);
                using GZipStream decompressStream = new(compressedStream, CompressionMode.Decompress);

                // Just go through everything and count bytes we are able to read
                Span<byte> buffer = stackalloc byte[1024 * 1024];
                long fileSize = 0;
                for (; ; )
                {
                    int read = decompressStream.Read(buffer);
                    if (read == 0)
                    {
                        break;
                    }
                    fileSize += read;
                }
                return fileSize == fileBlob.Size;
            }
            catch (Exception)
            {
                // File is declared as not good as soon as we have an exception...
                return false;
            }
        }

        /// <summary>
        /// Recursively traverse the given hierarchy and delete empty folders.
        /// </summary>
        /// <param name="path">Where to start the search.</param>
        static void CleanSubFolders(string path)
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                CleanSubFolders(directory);
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    try
                    {
                        Directory.Delete(directory, false);
                    }
                    catch
                    {
                        // Not that important, does not even worth a message, let's just skip...
                    }
                }
            }
        }

        /// <summary>
        /// Returns the storage folder to store a new file with the given size (uncompressed).
        /// </summary>
        /// <param name="bytes">Number of bytes we need to store in the folder.</param>
        /// <param name="excluding">We do not want to get that folder.</param>
        StorageFolderInfo? GetStorageFolderFor(long bytes, StorageFolderInfo? excluding = null)
        {
            var folders = m_StorageFolders.Values.OrderBy(sf => sf.Fullness);
            foreach (var folder in folders)
            {
                if (folder.FreeSpace >= bytes && folder != excluding)
                {
                    return folder;
                }
            }
            return null;
        }

        /// <summary>
        /// Wrapper to make reading content of a compressed <see cref="FileBlobInfo"/> easy.
        /// </summary>
        class DuplicateCandidate : IDisposable
        {
            public DuplicateCandidate(FileBlobInfo fileBlob)
            {
                Info = fileBlob;
                m_CompressedStream = File.OpenRead(fileBlob.Path);
                m_DecompressedStream = new GZipStream(m_CompressedStream, CompressionMode.Decompress);
            }

            /// <summary>
            /// Identifier of the corresponding <see cref="FileBlobInfo"/>.
            /// </summary>
            public FileBlobInfo Info { get; }

            /// <summary>
            /// Total number of bytes read from the candidate
            /// </summary>
            public long TotalReadBytes { get; private set; }

            /// <summary>
            /// Has we reached the end of the uncompressed <see cref="Stream"/>?
            /// </summary>
            public bool EndOfStreamReached { get; private set; }

            /// <summary>
            /// Data we currently have for this <see cref="DuplicateCandidate"/>.
            /// </summary>
            public ReadOnlyMemory<byte> CurrentData => m_ReadBuffer.CurrentData;

            /// <summary>
            /// Number of bytes we currently have for this <see cref="DuplicateCandidate"/>.
            /// </summary>
            public int CurrentDataLength => m_ReadBuffer.CurrentDataLength;

            /// <summary>
            /// Read bytes from the compressed stream returning a <see cref="Task"/> indicating reading is completed.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token</param>
            public async Task ReadBytesAsync(CancellationToken cancellationToken = default)
            {
                Memory<byte> readInto = m_ReadBuffer.RemainingBuffer;
                if (readInto.IsEmpty)
                {
                    return;
                }

                int readBytes = await m_DecompressedStream.ReadAsync(readInto, cancellationToken).ConfigureAwait(false);
                if (readBytes > 0)
                {
                    m_ReadBuffer.DataAdded(readBytes);
                    TotalReadBytes += readBytes;
                }
                else
                {
                    EndOfStreamReached = true;
                }
            }

            /// <summary>
            /// Indicate that the <paramref name="bytes"/> first bytes of <see cref="CurrentData"/> have been consumed.
            /// </summary>
            /// <param name="bytes">Number of bytes to consume from the start of <see cref="CurrentData"/>.</param>
            public void DataConsumed(int bytes)
            {
                m_ReadBuffer.DataConsumed(bytes);
            }

            public void Dispose()
            {
                m_DecompressedStream.Dispose();
                m_CompressedStream.Dispose();
            }

            BytesArrayWithWindow m_ReadBuffer = new(k_ReadChunkSize);
            Stream m_CompressedStream;
            GZipStream m_DecompressedStream;
        }

        /// <summary>
        /// Compress <paramref name="fileContent"/> in <paramref name="entry"/> while searching for potential duplicates
        /// among <paramref name="withSameMd5"/>.
        /// </summary>
        /// <param name="fileContent"><see cref="Stream"/> to the content of the file to compress.</param>
        /// <param name="fileLength">Length of the complete file provided by <paramref name="fileContent"/>.</param>
        /// <param name="entry"><see cref="FileBlobInfo"/> for the resulting compressed file.</param>
        /// <param name="withSameMd5"><see cref="FileBlobInfo"/> of other files with the same md5 checksum (that could
        /// potentially be equivalent to the file we are trying to compress).</param>
        /// <param name="expectedMd5">Expected md5 of <paramref name="fileContent"/>.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns><paramref name="entry"/> or one in <paramref name="withSameMd5"/> if a duplicate was found.
        /// </returns>
        async Task<FileBlobInfo> CompressFileAsync(Stream fileContent, long fileLength, FileBlobInfo entry,
            IEnumerable<FileBlobInfo> withSameMd5, Guid expectedMd5, CancellationToken cancellationToken)
        {
            List<DuplicateCandidate> duplicateCandidates = new(withSameMd5.Count());
            List<DuplicateCandidate> candidatesToRemove = new(withSameMd5.Count());
            Stream? writeCompressedStream = null;
            GZipStream? compressor = null;
            string entryPath = entry.Path;
            bool deleteCompressedFile = false;

            try
            {
                // Prepare reading from every FileBlobInfo with the same md5.
                foreach (var duplicateCandidate in withSameMd5)
                {
                    if (duplicateCandidate.Size == fileLength)
                    {
                        try
                        {
                            duplicateCandidates.Add(new(duplicateCandidate));
                        }
                        catch(Exception e)
                        {
                            m_Logger.LogWarning(e, "Failed to read from blob {Id} trying to find duplicates, will not " +
                                "consider it as a potential duplicate", duplicateCandidate.Id);
                        }
                    }
                }

                // To compute MD5 of the content while we are compressing it.
                using var md5Calculator = MD5.Create();

                // Prepare writing the compressed file (which we will delete if we find an equivalent FileBlobInfo or
                // keep if it is unique).  We write as we compare so that we do not have to read fileContent twice (but
                // yes it means that we might compress for nothing, we decided to favor reading only once at the expense
                // of some additional CPU usage or trying to fit everything in memory which wouldn't work with big
                // files).
                Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
                writeCompressedStream = File.OpenWrite(entryPath);
                writeCompressedStream.SetLength(0);
                compressor = new GZipStream(writeCompressedStream, CompressionMode.Compress);

                BytesArrayWithWindow fileContentReadBuffer = new(k_ReadChunkSize);
                List<Task> waitList = new(duplicateCandidates.Count + 1);
                for (; ; )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    waitList.Clear();

                    // Read from fileContent
                    Memory<byte> readInto = fileContentReadBuffer.RemainingBuffer;
                    ValueTask<int> readFromFileContentValueTask = fileContent.ReadAsync(readInto, cancellationToken);
                    Task<int> readFromFileContentTask = readFromFileContentValueTask.AsTask();
                    waitList.Add(readFromFileContentTask);

                    // Read from duplicateCandidates
                    foreach (var candidate in duplicateCandidates)
                    {
                        waitList.Add(candidate.ReadBytesAsync(cancellationToken));
                    }

                    // Wait for every file to have read some content
                    await Task.WhenAll(waitList).WaitAsync(cancellationToken);

                    // Find the minimum amount of bytes ready for everybody
                    if (fileContentReadBuffer.CurrentDataLength == 0 && readFromFileContentTask.Result == 0)
                    {
                        // End reached
                        break;
                    }
                    fileContentReadBuffer.DataAdded(readFromFileContentTask.Result);
                    int commonReadBytes = fileContentReadBuffer.CurrentDataLength;
                    foreach (var candidate in duplicateCandidates)
                    {
                        if (!candidate.EndOfStreamReached)
                        {
                            commonReadBytes = Math.Min(commonReadBytes, candidate.CurrentDataLength);
                        }
                    }

                    // Copy what we just read from the file to the stream feeding MD5 calculation
                    if (readFromFileContentTask.Result > 0)
                    {
                        int ret = md5Calculator.TransformBlock(fileContentReadBuffer.Storage,
                            fileContentReadBuffer.CurrentDataIndexInStorage, readFromFileContentTask.Result,
                            null, 0);
                        Debug.Assert(ret == readFromFileContentTask.Result);
                    }

                    // Process that same amount of bytes for everyone (compressing fileContent and comparing content of
                    // duplicate candidates).
                    var bytesToCompress = fileContentReadBuffer.CurrentData.Slice(0, commonReadBytes);
                    await compressor.WriteAsync(bytesToCompress, cancellationToken);
                    foreach (var candidate in duplicateCandidates)
                    {
                        if (!candidate.EndOfStreamReached)
                        {
                            ReadOnlyMemory<byte> bytesToCompare = candidate.CurrentData.Slice(0, commonReadBytes);
                            if (!bytesToCompress.Span.SequenceEqual(bytesToCompare.Span))
                            {
                                candidatesToRemove.Add(candidate);
                            }
                        }
                        else
                        {
                            m_Logger.LogWarning("FileBlob {Id} was shorter than expected ({Actual} vs {Expected})",
                                candidate.Info.Id, candidate.TotalReadBytes, candidate.Info.Size);
                            candidatesToRemove.Add(candidate);
                        }
                    }
                    foreach (var toRemove in candidatesToRemove)
                    {
                        duplicateCandidates.Remove(toRemove);
                        toRemove.Dispose();
                    }
                    candidatesToRemove.Clear();

                    // Move content after the consumed part
                    fileContentReadBuffer.DataConsumed(commonReadBytes);
                    foreach (var candidate in duplicateCandidates)
                    {
                        candidate.DataConsumed(commonReadBytes);
                    }
                }

                // If we reach the end and still have a candidate that also reached the end then it means this is a
                // duplicate.
                if (duplicateCandidates.Count > 0)
                {
                    foreach (var candidate in duplicateCandidates)
                    {
                        if (candidate.EndOfStreamReached && candidate.CurrentDataLength == 0)
                        {
                            compressor.Close();
                            writeCompressedStream.Close();
                            File.Delete(entry.Path);
                            return candidate.Info;
                        }
                        else
                        {
                            m_Logger.LogWarning("FileBlob {Id} was longer than expected", candidate.Info.Id);
                        }
                    }
                }

                // Conclude MD5 calculation
                md5Calculator.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                Debug.Assert(md5Calculator.Hash != null);
                byte[] computedHashBytes = md5Calculator.Hash;
                Debug.Assert(computedHashBytes.Length == 16);
                Guid computeMd5 = new(computedHashBytes);
                if (computeMd5 != expectedMd5)
                {
                    throw new ArgumentException($"Expected MD5 of file blob to be " +
                        $"{Convert.ToHexString(expectedMd5.ToByteArray())} but it was " +
                        $"{Convert.ToHexString(computedHashBytes)}");
                }

                return entry;
            }
            catch (Exception)
            {
                // Something bad happened, we will need to delete the compressed file (after disposing of the objects
                // using it).
                deleteCompressedFile = true;
                throw;
            }
            finally
            {
                if (compressor != null)
                {
                    await compressor.DisposeAsync();
                }
                if (writeCompressedStream != null)
                {
                    await writeCompressedStream.DisposeAsync();
                }
                foreach (var duplicateCandidate in duplicateCandidates)
                {
                    duplicateCandidate.Dispose();
                }
                if (deleteCompressedFile && File.Exists(entryPath))
                {
                    try
                    {
                        File.Delete(entryPath);
                    }
                    catch(Exception e)
                    {
                        m_Logger.LogError(e, "Failed to delete compressed file {Path}, will be cleared on next restart",
                            entryPath);
                        try
                        {
                            FileInfo zombieInfo = new(entryPath);
                            entry.StorageFolder!.CountZombies(zombieInfo.Length);
                        }
                        catch (Exception e2)
                        {
                            m_Logger.LogError(e2, "Failed to get information about {File}, StorageFolder size might " +
                                "be wrong", entryPath);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new <see cref="FileBlobLock"/> for the given <see cref="FileBlobInfo"/>.
        /// </summary>
        /// <param name="fileBlobInfo">The <see cref="FileBlobInfo"/>.</param>
        /// <remarks>Assumes the caller has locked <see cref="m_Lock"/>.</remarks>
        FileBlobLock CreateFileBlobLock(FileBlobInfo fileBlobInfo)
        {
            Debug.Assert(Monitor.IsEntered(m_Lock));

            TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            fileBlobInfo.Using.Add(tcs.Task);
            return new FileBlobLock(fileBlobInfo, () => {
                lock (m_Lock)
                {
                    fileBlobInfo.Using.Remove(tcs.Task);
                    tcs.TrySetResult();

                    CheckForConcludeFileBlobMove(fileBlobInfo);
                }
            });
        }

        /// <summary>
        /// Try to move the given <see cref="FileBlobInfo"/> to another storage folder.
        /// </summary>
        /// <param name="filesToMove">List of files to try to move</param>
        /// <exception cref="StorageFolderFullException">If we ran out of space while trying to perform the move.</exception>
        /// <remarks>Some move might not be feasible right now and need to be retried when the file will be ready.</remarks>
        async Task TryToMoveFilesAsync(IEnumerable<FileBlobInfo> filesToMove)
        {
            // Move the files
            List<Task> nonReadyFilesTasks = new();
            foreach (var fileToMove in filesToMove)
            {
                // Don't start another move if the file is already being moved...
                if (m_PendingMoves.ContainsKey(fileToMove.Id))
                {
                    continue;
                }

                // Get (and lock) the file to move
                FileBlobLock? fromLockedFileBlobTemp;
                try
                {
                    lock (m_Lock)
                    {
                        // This will throw a KeyNotFoundException if not found and this is exactly what we want.
                        var validatedFileToMove = m_IdDictionary[fileToMove.Id];
                        if (!validatedFileToMove.IsReady)
                        {
                            // File is being produced, not ready to be moved yet.  Continue with the other files
                            nonReadyFilesTasks.AddRange(validatedFileToMove.Using);
                            continue;
                        }
                        fromLockedFileBlobTemp = CreateFileBlobLock(validatedFileToMove);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Looks like someone got rid of the file we had to move (while we were unlocked), just skip it.
                    continue;
                }

                FileBlobInfo newFileBlobInfo;
                TaskCompletionSource fileMovedTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using (var fromLockedFileBlob = fromLockedFileBlobTemp)
                {
                    // ReSharper disable once RedundantAssignment -> We set it to null so that no one else in the code
                    // below uses it, everyone should now use fromLockedFileBlob.
                    fromLockedFileBlobTemp = null;

                    // Prepare an entry in the new storage folder
                    string toCopyToPath;
                    lock (m_Lock)
                    {
                        var newStorageFolder = GetStorageFolderFor(fromLockedFileBlob.CompressedSize,
                            fileToMove.StorageFolder);
                        if (newStorageFolder == null)
                        {
                            throw new StorageFolderFullException(
                                $"Remaining storage folders do not have enough space (" +
                                $"{fromLockedFileBlob.CompressedSize} bytes) to store file blob " +
                                $"{fromLockedFileBlob.Id}.");
                        }

                        newFileBlobInfo = new(fromLockedFileBlob.Id, fromLockedFileBlob.Md5,
                            fromLockedFileBlob.CompressedSize, fromLockedFileBlob.Size);
                        newFileBlobInfo.StorageFolder = newStorageFolder;
                        newStorageFolder.AddFile(newFileBlobInfo);
                        newFileBlobInfo.Using.Add(fileMovedTaskCompletionSource.Task);

                        toCopyToPath = newFileBlobInfo.Path;
                    }

                    // Do the actual copy
                    // Remark: We do a copy and not a move.  1st, it makes the code easier (since we don't have to wait
                    // for the original file blob to not be in use) but 2nd, chances are moving is not performed on the
                    // same disk, so it is the same speed anyway.
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(toCopyToPath)!);
                        File.Copy(fromLockedFileBlob.Path, toCopyToPath);
                    }
                    catch (Exception)
                    {
                        if (File.Exists(toCopyToPath))
                        {
                            DeleteFileOfBlobInfo(newFileBlobInfo);
                        }

                        lock (m_Lock)
                        {
                            newFileBlobInfo.StorageFolder.RemoveFile(newFileBlobInfo);
                            fileMovedTaskCompletionSource.TrySetResult();
                        }
                        throw;
                    }
                }

                // Try to conclude the move (will either be done now of queued for when the from files will not be
                // locked anymore).
                lock (m_Lock)
                {
                    ConcludeFileBlobMove(fileToMove.Id, fileToMove.StorageFolder!.FullPath,
                        newFileBlobInfo.StorageFolder!.FullPath, fileMovedTaskCompletionSource);
                }
            }

            // Wait for files pending to move to be moved
            var tasksToWaitOn = m_PendingMoves.Values.Select(pm => pm.ToFileBlobInfoReady!.Task);
            tasksToWaitOn = tasksToWaitOn.Concat(nonReadyFilesTasks);
            await Task.WhenAll(tasksToWaitOn);
        }

        /// <summary>
        /// Complete file blob move operations waiting for the provided <see cref="FileBlobInfo"/> to not be used
        /// anymore.
        /// </summary>
        /// <param name="fileBlobInfo">The <see cref="FileBlobInfo"/> that stopped being used.</param>
        /// <remarks>This method should be called every time a task is removed from <see cref="FileBlobInfo.Using"/>.
        /// </remarks>
        void CheckForConcludeFileBlobMove(FileBlobInfo fileBlobInfo)
        {
            if (fileBlobInfo.Using.Any())
            {
                return;
            }

            if (!m_PendingMoves.TryGetValue(fileBlobInfo.Id, out var pendingMove))
            {
                return;
            }
            m_PendingMoves.Remove(fileBlobInfo.Id);

            ConcludeFileBlobMove(pendingMove.Id, pendingMove.FromStorageFolder, pendingMove.ToStorageFolder,
                pendingMove.ToFileBlobInfoReady!);
        }

        /// <summary>
        /// Conclude moving file blob <paramref name="fileBlobId"/> from <paramref name="fromStorageFolderPath"/> to
        /// <paramref name="toStorageFolderPath"/>.
        /// </summary>
        /// <param name="fileBlobId">Identifier of the moved file blob.</param>
        /// <param name="fromStorageFolderPath">Full path of the storage folder from which the file was moved.</param>
        /// <param name="toStorageFolderPath">Full path of the storage folder to which the file was moved.</param>
        /// <param name="toSignalWhenConcluded"><see cref="TaskCompletionSource"/> to signal when completed.</param>
        void ConcludeFileBlobMove(Guid fileBlobId, string fromStorageFolderPath, string toStorageFolderPath,
                                  TaskCompletionSource toSignalWhenConcluded)
        {
            if (!m_StorageFolders.ContainsKey(fromStorageFolderPath))
            {
                throw new InvalidOperationException($"Can't find move from storage folder {fromStorageFolderPath}");
            }
            if (!m_StorageFolders.TryGetValue(toStorageFolderPath, out var toStorageFolder))
            {
                throw new InvalidOperationException($"Can't find move to storage folder {toStorageFolderPath}");
            }
            if (!toStorageFolder.TryGetFile(fileBlobId, out var toFileBlobInfo))
            {
                throw new InvalidOperationException($"Trying to conclude move of {fileBlobId} but cannot find it in " +
                    $"the destination folder");
            }

            if (!m_IdDictionary.TryGetValue(fileBlobId, out var fromFileBlobInfo))
            {
                // Unknown fileBlobId?  This is strange.  I guess someone delete the blob while we were moving it...
                // Check in the to storage folder to remove it.
                DeleteFileOfBlobInfo(toFileBlobInfo);
                toFileBlobInfo.StorageFolder!.RemoveFile(toFileBlobInfo);
                toSignalWhenConcluded.TrySetResult();
                return;
            }

            if (fromFileBlobInfo.Using.Count > 0)
            {
                // The original file is still in use, we cannot conclude yet, postpone to latter.
                m_PendingMoves[fileBlobId] = new PendingMove() {
                    Id = fileBlobId,
                    FromStorageFolder = fromStorageFolderPath,
                    ToStorageFolder = toStorageFolderPath,
                    ToFileBlobInfoReady = toSignalWhenConcluded
                };
                return;
            }

            // Discard fromFileBlobInfo
            DeleteFileOfBlobInfo(fromFileBlobInfo);
            fromFileBlobInfo.StorageFolder!.RemoveFile(fromFileBlobInfo);

            // Mark the toFileBlob as completed
            toFileBlobInfo.IsReady = true;
            toFileBlobInfo.Using.Clear();
            toFileBlobInfo.ReferenceCount = fromFileBlobInfo.ReferenceCount;
            toSignalWhenConcluded.TrySetResult();

            // Replace fromFileBlobInfo by toFileBlobInfo in the dictionaries
            m_IdDictionary[fileBlobId] = toFileBlobInfo;
            var filesBlobsWithSameMd5 = GetFileBlobsWithMd5(fromFileBlobInfo.Md5);
            filesBlobsWithSameMd5 = filesBlobsWithSameMd5.Where(fbi => fbi != fromFileBlobInfo);
            filesBlobsWithSameMd5 = filesBlobsWithSameMd5.Append(toFileBlobInfo);
            SetFileBlobsWithMd5(toFileBlobInfo.Md5, filesBlobsWithSameMd5);
        }

        /// <summary>
        /// Abort pending file move (in <see cref="m_PendingMoves"/>).
        /// </summary>
        /// <remarks>Caller should have locked <see cref="m_Lock"/> before calling this method.</remarks>
        void AbortPendingFileMove()
        {
            foreach (var pendingMove in m_PendingMoves.Values)
            {
                if (m_StorageFolders.TryGetValue(pendingMove.ToStorageFolder, out var toStorageFolder))
                {
                    if (toStorageFolder.TryGetFile(pendingMove.Id, out var toFileBlobInfo))
                    {
                        DeleteFileOfBlobInfo(toFileBlobInfo);
                    }
                }
            }
            m_PendingMoves.Clear();
        }

        /// <summary>
        /// Delete the file of a FileBlobInfo (and add it to zombies if there is an error).
        /// </summary>
        /// <param name="fileBlobInfo">Blob that we want to delete the file of.</param>
        void DeleteFileOfBlobInfo(FileBlobInfo fileBlobInfo)
        {
            string toDeletePath = fileBlobInfo.Path;
            try
            {
                if (File.Exists(toDeletePath))
                {
                    File.Delete(toDeletePath);
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to delete {Path}, will be added to zombies that we will " +
                    "try to remove on next restart", toDeletePath);
                fileBlobInfo.StorageFolder!.CountZombies(fileBlobInfo.CompressedSize);
            }
        }

        /// <summary>
        /// Returns all the <see cref="FileBlobInfo"/> with the given md5 checksum.
        /// </summary>
        /// <param name="md5">The md5 checksum.</param>
        IEnumerable<FileBlobInfo> GetFileBlobsWithMd5(Guid md5)
        {
            List<FileBlobInfo> ret = new();
            if (m_Md5Dictionary.TryGetValue(md5, out var fileWithSameMd5))
            {
                while (fileWithSameMd5 != null)
                {
                    ret.Add(fileWithSameMd5);
                    fileWithSameMd5 = fileWithSameMd5.NextInSameMd5Chain;
                }
            }
            return ret;
        }

        /// <summary>
        /// Sets the list of <see cref="FileBlobInfo"/> with the given md5 checksum.
        /// </summary>
        /// <param name="md5">The md5 checksum.</param>
        /// <param name="fileBlobInfos">The list of <see cref="FileBlobInfo"/>.</param>
        void SetFileBlobsWithMd5(Guid md5, IEnumerable<FileBlobInfo> fileBlobInfos)
        {
            FileBlobInfo? first = fileBlobInfos.FirstOrDefault();
            if (first == null)
            {
                m_Md5Dictionary.Remove(md5);
                return;
            }
            // Validate we are adding FileBlobInfo that are really present in a storage folder.
            Debug.Assert(first.StorageFolder != null &&
                         first.StorageFolder.TryGetFile(first.Id, out var blobInfoFromFolder1) &&
                         blobInfoFromFolder1 == first);

            FileBlobInfo previous = first;
            foreach(var fileBlobInfo in fileBlobInfos.Skip(1))
            {
                // Validate we are adding FileBlobInfo that are really present in a storage folder.
                Debug.Assert(fileBlobInfo.StorageFolder != null &&
                             fileBlobInfo.StorageFolder.TryGetFile(fileBlobInfo.Id, out var blobInfoFromFolder2) &&
                             blobInfoFromFolder2 == fileBlobInfo);

                previous.NextInSameMd5Chain = fileBlobInfo;
                previous = fileBlobInfo;
            }
            previous.NextInSameMd5Chain = null;

            m_Md5Dictionary[md5] = first;
        }

        /// <summary>
        /// Add a <see cref="FileBlobInfo"/> to <see cref="m_Md5Dictionary"/>.
        /// </summary>
        /// <param name="toAdd">To add</param>
        // ReSharper disable once UnusedMember.Local -> So that we have the add for RemoveFileBlobInfoFromMd5Dictionary
        void AddFileBlobInfoToMd5Dictionary(FileBlobInfo toAdd)
        {
            SetFileBlobsWithMd5(toAdd.Md5, GetFileBlobsWithMd5(toAdd.Md5).Append(toAdd));
        }

        /// <summary>
        /// Remove the given <see cref="FileBlobInfo"/> from <see cref="m_Md5Dictionary"/>.
        /// </summary>
        /// <param name="toRemove">To remove</param>
        void RemoveFileBlobInfoFromMd5Dictionary(FileBlobInfo toRemove)
        {
            SetFileBlobsWithMd5(toRemove.Md5, GetFileBlobsWithMd5(toRemove.Md5).Where(fbi => fbi != toRemove));
        }

        /// <summary>
        /// Information about a delayed file blob move.
        /// </summary>
        /// <remarks>Some file blob move cannot be concluded immediately after the actual file was copied (because the
        /// from file is locked).  When that is the case an entry like this one is created and added to
        /// <see cref="m_PendingMoves"/> to be processed on unlock.
        /// </remarks>
        class PendingMove
        {
            /// <summary>
            /// Identifier of the file blob we are moving
            /// </summary>
            public Guid Id { get; init; }

            /// <summary>
            /// Storage folder (full path of, key in <see cref="m_StorageFolders"/>) from which we are moving the file
            /// blob.
            /// </summary>
            public string FromStorageFolder { get; init; } = "";

            /// <summary>
            /// Storage folder (full path of, key in <see cref="m_StorageFolders"/>) to which we are moving the file
            /// blob.
            /// </summary>
            public string ToStorageFolder { get; init; } = "";

            /// <summary>
            /// TaskCompletionSource to be signaled when move is completed (when the <see cref="FileBlobInfo"/> in
            /// <see cref="ToStorageFolder"/> becomes ready).
            /// </summary>
            public TaskCompletionSource? ToFileBlobInfoReady { get; init; }
        };

        readonly ILogger m_Logger;

        /// <summary>
        /// Used to be sure we are performing task moving files around from a single thread at the time.
        /// </summary>
        readonly AsyncLock m_MovingFilesLock = new();

        /// <summary>
        /// Internal object used to lock access to member variables of this class
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// The folders in which we are storing files.
        /// </summary>
        readonly Dictionary<string, StorageFolderInfo> m_StorageFolders = new();

        /// <summary>
        /// Dictionary indexing FileBlobInfo through their identifiers.
        /// </summary>
        readonly Dictionary<Guid, FileBlobInfo> m_IdDictionary = new();

        /// <summary>
        /// Dictionary indexing FileBlobInfo through their md5 checksum.
        /// </summary>
        /// <remarks>I know, the key is not a byte[] or string but a <see cref="Guid"/>.  This is because both md5
        /// hash and <see cref="Guid"/> are 128 bits and this is an easy and efficient way to get a
        /// <see cref="Dictionary{TKey, TValue}"/> key.</remarks>
        readonly Dictionary<Guid, FileBlobInfo> m_Md5Dictionary = new();

        /// <summary>
        /// Pending file blob moves.
        /// </summary>
        readonly Dictionary<Guid, PendingMove> m_PendingMoves = new();

        /// <summary>
        /// Let's try to read by chunks of 2 megs.
        /// </summary>
        /// <remarks>to avoid reading in too small chunks (a lot of overhead) and take too much memory when compressing
        /// many files in parallel.</remarks>
        const int k_ReadChunkSize = 2 * 1024 * 1024;
    }
}
