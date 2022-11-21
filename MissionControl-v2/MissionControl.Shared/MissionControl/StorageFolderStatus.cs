namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Class used to transmit the status of a MissionControl's folder where it stores the asset files.
    /// </summary>
    public class StorageFolderStatus: IEquatable<StorageFolderStatus>
    {
        /// <summary>
        /// Path to the folder managed by the Hangar Bay in which it stores cached files.
        /// </summary>
        /// <remarks>Matches the Path of <see cref="StorageFolderConfig"/>.</remarks>
        public string Path { get; set; } = "";

        /// <summary>
        /// Current size in bytes of all the files in the folder (including zombies).
        /// </summary>
        public long CurrentSize { get; set; }

        /// <summary>
        /// Size of files that shouldn't be in that folder and that couldn't be deleted.
        /// </summary>
        public long ZombiesSize { get; set; }

        /// <summary>
        /// Maximum number of bytes to be used by files in the StorageFolder.
        /// </summary>
        public long MaximumSize { get; set; }

        /// <summary>
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void DeepCopy(StorageFolderStatus from)
        {
            Path = from.Path;
            CurrentSize = from.CurrentSize;
            ZombiesSize = from.ZombiesSize;
            MaximumSize = from.MaximumSize;
        }

        public bool Equals(StorageFolderStatus? other)
        {
            return other != null &&
                Path == other.Path &&
                CurrentSize == other.CurrentSize &&
                ZombiesSize == other.ZombiesSize &&
                MaximumSize == other.MaximumSize;
        }
    }
}
