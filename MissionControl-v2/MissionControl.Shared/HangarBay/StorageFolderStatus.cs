namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Class used to transmit the status of a Hangar Bay's folder where it stores the cached files.
    /// </summary>
    public class StorageFolderStatus
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
        /// Current size in bytes of all the unreferenced files in the folder (including zombies).
        /// </summary>
        public long UnreferencedSize { get; set; }

        /// <summary>
        /// Current size in bytes of all the zombies in the folder (file that couldn't be deleted).
        /// </summary>
        public long ZombiesSize { get; set; }

        /// <summary>
        /// Maximum number of bytes to be used by files in the StorageFolder.
        /// </summary>
        public long MaximumSize { get; set; }

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(StorageFolderStatus))
            {
                return false;
            }
            var other = (StorageFolderStatus)obj;

            return Path == other.Path && CurrentSize == other.CurrentSize &&
                UnreferencedSize == other.UnreferencedSize && ZombiesSize == other.ZombiesSize &&
                MaximumSize == other.MaximumSize;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Path, MaximumSize, UnreferencedSize, ZombiesSize, MaximumSize);
        }
    }
}
