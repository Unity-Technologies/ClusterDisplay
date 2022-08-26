namespace Unity.ClusterDisplay.MissionControl.HangarBay.Library
{
    /// <summary>
    /// Hangar Bay configuration.
    /// </summary>
    public class Config
    {
        /// <summary>
        /// End point to which to listen for REST commands (most likely coming from LaunchPads or MissionControl).
        /// </summary>
        /// <remarks>Only protocol, host and port (eg. http://localhost:8100), does not include any "common" path in
        /// the URL (not http://localhost:8100/api/v1).</remarks>
        public IEnumerable<String> ControlEndPoint { get; set; } = new[] { "http://localhost:8100" };

        /// <summary>
        /// List of folders of where we store files (used as a cache to avoid constantly fetching from MissionControl).
        /// </summary>
        public IEnumerable<StorageFolderConfig> StorageFolders { get; set; } = new StorageFolderConfig[] {};
    }
}
