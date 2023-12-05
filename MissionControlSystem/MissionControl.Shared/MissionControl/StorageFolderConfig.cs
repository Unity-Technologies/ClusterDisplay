namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Class used to specify the configuration of a MissionControl's folder where it stores the asset files.
    /// </summary>
    public class StorageFolderConfig: IEquatable<StorageFolderConfig>
    {
        /// <summary>
        /// Path to the folder that will be managed by MissionControl and in which it will store the assets files.
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
