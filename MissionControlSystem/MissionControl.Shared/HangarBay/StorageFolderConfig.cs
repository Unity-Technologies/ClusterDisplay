namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Class used to specify the configuration of an Hangar Bay's folder where it stores the cached files.
    /// </summary>
    public class StorageFolderConfig: IEquatable<StorageFolderConfig>
    {
        /// <summary>
        /// Path to the folder that will be managed by the Hangar Bay and in which it will store its cached files.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Maximum number of bytes to be used by files in the StorageFolder.
        /// </summary>
        public long MaximumSize { get; set; }

        public bool Equals(StorageFolderConfig? other)
        {
            return other != null &&
                Path == other.Path &&
                MaximumSize == other.MaximumSize;
        }
    }
}
