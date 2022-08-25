using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Information about a storage folder
    /// </summary>
    class StorageFolderInfo
    {
        /// <summary>
        /// Path of where to store the files as provided to the user (so that he can see the path the way he
        /// provided it).
        /// </summary>
        public string UserPath { get; set; } = "";

        /// <summary>
        /// Full effective path to the folder.
        /// </summary>
        [JsonIgnore]
        public string FullPath { get; set; } = "";

        /// <summary>
        /// Files that are currently not used by anybody and are waiting in the cache to be either used or deleted
        /// because too old and we need space.
        /// </summary>
        public FileInfoLinkedList InCache { get; set;  } = new();

        /// <summary>
        /// Files that are currently the folder but not referenced by anyone.  They are kept in cast some other
        /// payload need them but they will be the first ones to go if some free space is needed.
        /// </summary>
        public FileInfoLinkedList Unreferenced { get; set; } = new();

        /// <summary>
        /// Files currently in use (either being fetched or copied to a LaunchPad folder).
        /// </summary>
        public FileInfoLinkedList InUse { get; set; } = new();

        /// <summary>
        /// List of files that we were not able to delete and are still in the storage folder.
        /// </summary>
        public List<Guid> Zombies { get; set; } = new();

        /// <summary>
        /// Size (in bytes) of all the file <see cref="CacheFileInfo"/> in our different lists.
        /// </summary>
        [JsonIgnore]
        public long FileInfoSize { get => InCache.CompressedSize + Unreferenced.CompressedSize + InUse.CompressedSize; }

        /// <summary>
        /// Size (in bytes) of all the zombies
        /// </summary>
        [JsonIgnore]
        public long ZombiesSize { get; set; }

        /// <summary>
        /// Size of all the files in the storage folder (except the metadata json).
        /// </summary>
        [JsonIgnore]
        public long EffectiveSize { get => FileInfoSize + ZombiesSize; }

        /// <summary>
        /// Maximum number of bytes to be used by files in the StorageFolder.
        /// </summary>
        [JsonIgnore]
        public long MaximumSize { get; set; }

        /// <summary>
        /// How much free space there is in the folder?
        /// </summary>
        [JsonIgnore]
        public long FreeSpace { get => MaximumSize - EffectiveSize; }

        /// <summary>
        /// Does the metadata about the file information need to be updated?
        /// </summary>
        [JsonIgnore]
        public bool NeedSaving { get; set; }

        /// <summary>
        /// Returns the path within the storage to the file blob with the given identifier.
        /// </summary>
        /// <param name="fileBlobId">File blob identifier</param>
        /// <returns>Full path to the file.</returns>
        public string GetPath(Guid fileBlobId)
        {
            var filenameAsString = fileBlobId.ToString();
            return Path.Combine(FullPath, filenameAsString.Substring(0, 2), filenameAsString.Substring(2, 2), filenameAsString);
        }

        /// <summary>
        /// Delete the given file blob.
        /// </summary>
        /// <param name="logger">Logger to use in case of error.</param>
        /// <param name="fileBlobInfo">File blob information.</param>
        public void DeleteFile(ILogger logger, Guid fileBlobInfo)
        {
            string filePath = GetPath(fileBlobInfo);
            try
            {
                File.Delete(filePath);
            }
            catch (Exception)
            {
                logger.LogWarning($"Failed to delete {fileBlobInfo} from {UserPath}, will be added to zombies.");
                Zombies.Add(fileBlobInfo);

                try
                {
                    var dotNetFileInfo = new System.IO.FileInfo(filePath);
                    ZombiesSize += dotNetFileInfo.Length;
                }
                catch(Exception) {}
            }
        }

        /// <summary>
        /// Evict content from the folder so that it fits in budget.
        /// </summary>
        /// <param name="logger">Logger to use in case of error.</param>
        public void EvictsToFitInBudget(ILogger logger)
        {
            while (EffectiveSize > MaximumSize)
            {
                if (Unreferenced.First != null)
                {
                    EvictNextUnrefrenced(logger);
                }
                else if (InCache.First != null)
                {
                    EvictNextInCache(logger);
                }
                else
                {
                    // Looks like we can't evict anything, will be done later I guess...
                    return;
                }
            }
        }

        /// <summary>
        /// Deletes the next unreferenced file (caller should check there is at least one before calling this
        /// method).
        /// </summary>
        /// <param name="logger">Logger to use in case of error.</param>
        public void EvictNextUnrefrenced(ILogger logger)
        {
            var toRemove = Unreferenced.First!.Value;
            Debug.Assert(toRemove.CopyTasks.Count == 0);
            Debug.Assert(toRemove.FetchTask == null);
            Debug.Assert(toRemove.StorageFolder == this);
            Debug.Assert(toRemove.NodeInList == Unreferenced.First);
            toRemove.StorageFolder = null;
            toRemove.NodeInList = null;
            Unreferenced.RemoveFirst();

            DeleteFile(logger, toRemove.Id);
        }

        /// <summary>
        /// Deletes the next referenced file in the cache (caller should check there is at least one before calling
        /// this method).
        /// </summary>
        /// <param name="logger">Logger to use in case of error.</param>
        public void EvictNextInCache(ILogger logger)
        {
            var toRemove = InCache.First!.Value;
            Debug.Assert(toRemove.CopyTasks.Count == 0);
            Debug.Assert(toRemove.FetchTask == null);
            toRemove.StorageFolder = null;
            toRemove.NodeInList = null;
            InCache.RemoveFirst();

            DeleteFile(logger, toRemove.Id);
        }
    }
}
