using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Information about a file blob storage folder
    /// </summary>
    class StorageFolderInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public StorageFolderInfo() { }

        /// <summary>
        /// Constructor to be used by json de-serialization.
        /// </summary>
        /// <param name="files">Files in the storage folder.</param>
        [JsonConstructor]
        public StorageFolderInfo(IReadOnlyCollection<FileBlobInfo> files)
        {
            foreach (var file in files)
            {
                file.StorageFolder = this;
                m_IdDictionary.Add(file.Id, file);
                Size += file.CompressedSize;
            }
        }

        /// <summary>
        /// Path of where to store the files as provided to the user (so that he can see the path the way he
        /// provided it when making queries to MissionControl).
        /// </summary>
        [JsonIgnore]
        public string UserPath { get; set; } = "";

        /// <summary>
        /// Full effective path to the folder.
        /// </summary>
        [JsonIgnore]
        public string FullPath { get; set; } = "";

        /// <summary>
        /// Files in the storage folder.
        /// </summary>
        public IReadOnlyCollection<FileBlobInfo> Files => m_IdDictionary.Values;

        /// <summary>
        /// Size of all the files (compressed size) in the storage folder (except the metadata json).
        /// </summary>
        /// <remarks>Include <see cref="ZombiesSize"/>.</remarks>
        [JsonIgnore]
        public long Size { get; private set; }

        /// <summary>
        /// Size of zombies (files that couldn't be deleted when needed)
        /// </summary>
        [JsonIgnore]
        public long ZombiesSize { get; private set; }

        /// <summary>
        /// Maximum number of bytes to be used by files in the StorageFolder.
        /// </summary>
        [JsonIgnore]
        public long MaximumSize { get; set; }

        /// <summary>
        /// Percentage of use of the folder (0.0f for empty, 1.0f for full).
        /// </summary>
        [JsonIgnore]
        public float Fullness => (float)((double)Size / MaximumSize);

        /// <summary>
        /// How much free space there is in the folder?
        /// </summary>
        [JsonIgnore]
        public long FreeSpace => MaximumSize - Size;

        /// <summary>
        /// Does the metadata about the file information need to be updated?
        /// </summary>
        [JsonIgnore]
        public bool NeedSaving { get; private set; } = true;

        /// <summary>
        /// Indicate the <see cref="StorageFolderInfo"/> has been saved (sets NeedSaving to false).
        /// </summary>
        public void SetSaved()
        {
            NeedSaving = false;
        }

        /// <summary>
        /// Returns the path to the file that stores the metadata for a storage folder.
        /// </summary>
        /// <param name="storageFolderFullPath"><see cref="StorageFolderInfo.FullPath"/>.
        /// </param>
        public static string GetMetadataFilePath(string storageFolderFullPath)
        {
            return Path.Combine(storageFolderFullPath, k_MetadataJson);
        }

        /// <summary>
        /// Returns the path to the file that stores the metadata of this storage folder.
        /// </summary>
        public string GetMetadataFilePath()
        {
            return GetMetadataFilePath(FullPath);
        }

        /// <summary>
        /// Adds a <see cref="FileBlobInfo"/> to the <see cref="StorageFolderInfo"/>.
        /// </summary>
        /// <param name="toAdd"><see cref="FileBlobInfo"/> to add.</param>
        public void AddFile(FileBlobInfo toAdd)
        {
            m_IdDictionary.Add(toAdd.Id, toAdd);
            Size += toAdd.CompressedSize;
            NeedSaving = true;
        }

        /// <summary>
        /// Removes a <see cref="FileBlobInfo"/> from the <see cref="StorageFolderInfo"/>.
        /// </summary>
        /// <param name="toRemove"><see cref="FileBlobInfo"/> to remove.</param>
        public void RemoveFile(FileBlobInfo toRemove)
        {
            if (m_IdDictionary.Remove(toRemove.Id))
            {
                Size -= toRemove.CompressedSize;
                NeedSaving = true;
            }
        }

        /// <summary>
        /// Try to get the <see cref="FileBlobInfo"/> with the specified identifier.
        /// </summary>
        /// <param name="id">Identifier</param>
        /// <param name="fileBlobInfo">Found <see cref="FileBlobInfo"/>.</param>
        /// <returns>Have we found something?</returns>
        public bool TryGetFile(Guid id, [MaybeNullWhen(false)] out FileBlobInfo fileBlobInfo)
        {
            return m_IdDictionary.TryGetValue(id, out fileBlobInfo);
        }

        /// <summary>
        /// Returns if the <see cref="StorageFolderInfo"/> contains a <see cref="FileBlobInfo"/> with the given
        /// identifier.
        /// </summary>
        /// <param name="id"><see cref="FileBlobInfo"/> identifier.</param>
        public bool ContainsFileBlob(Guid id)
        {
            return m_IdDictionary.ContainsKey(id);
        }

        /// <summary>
        /// Inform the folder about a zombie.
        /// </summary>
        /// <param name="zombieSize">Size of the zombie in bytes.</param>
        public void CountZombies(long zombieSize)
        {
            // For now we only care about the size of those zombies...
            ZombiesSize += zombieSize;
            Size += zombieSize;
        }

        const string k_MetadataJson = "metadata.json";

        /// <summary>
        /// List of all the files in the storage folder.
        /// </summary>
        readonly Dictionary<Guid, FileBlobInfo> m_IdDictionary = new();
    }
}
