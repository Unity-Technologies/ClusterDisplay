using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Information about a file in the cache.
    /// </summary>
    /// <remarks>We can have a file information without the actual physical file to "back it up".  This will happen
    /// when referencing a file before adding the storage folders and can be identified by
    /// <see cref="StorageFolder"/> that is null.</remarks>
    class CacheFileInfo
    {
        /// <summary>
        /// Identifier of the FileBob this FileInfo is about.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Size (in bytes) of the compressed file.
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Size (in bytes) of the uncompressed size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last time the file was used
        /// </summary>
        public DateTime LastAccess { get; set; } = DateTime.MinValue;

        /// <summary>
        /// How many payloads are using this file?
        /// </summary>
        [JsonIgnore]
        public long ReferenceCount { get; set; }

        /// <summary>
        /// Storage folder the file is stored in.
        /// </summary>
        [JsonIgnore]
        public StorageFolderInfo? StorageFolder { get; set; }

        /// <summary>
        /// Position of this FileInfo in one of the list of <see cref="StorageFolder"/>.
        /// </summary>
        [JsonIgnore]
        public LinkedListNode<CacheFileInfo>? NodeInList { get; set; }

        /// <summary>
        /// Task currently fetching the file from Mission Control.
        /// </summary>
        [JsonIgnore]
        public Task? FetchTask { get; set; }

        /// <summary>
        /// Tasks currently copying the file (to a LaunchPad folder)
        /// </summary>
        [JsonIgnore]
        public List<Task> CopyTasks { get; } = new();

        /// <summary>
        /// <see cref="FetchTask"/> and <see cref="CopyTasks"/>.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Task> AllTasks { get => FetchTask != null ? CopyTasks.Append(FetchTask) : CopyTasks; }
    }
}
