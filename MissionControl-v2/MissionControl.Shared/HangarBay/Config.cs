namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// HangarBay's configuration.  This data is also the content of the config.json file used to store the
    /// configuration of the service.
    /// </summary>
    public struct Config: IEquatable<Config>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public Config() { }

        /// <summary>
        /// End points to which to listen for REST commands (most likely coming from LaunchPads or MissionControl).
        /// </summary>
        public IEnumerable<string> ControlEndPoints { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// List of folders of where we store files (used as a cache to avoid constantly fetching from MissionControl).
        /// </summary>
        public IEnumerable<StorageFolderConfig> StorageFolders { get; set; } = Enumerable.Empty<StorageFolderConfig>();

        public bool Equals(Config other)
        {
            if (other.GetType() != typeof(Config))
            {
                return false;
            }

            return ControlEndPoints.SequenceEqual(other.ControlEndPoints) &&
                StorageFolders.SequenceEqual(other.StorageFolders);
        }
    }
}
