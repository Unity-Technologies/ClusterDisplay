namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// HangarBay's configuration.  This data is also the content of the config.json file used to store the
    /// configuration of the service.
    /// </summary>
    public struct Config
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

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(Config))
            {
                return false;
            }
            var other = (Config)obj;

            return ControlEndPoints.SequenceEqual(other.ControlEndPoints) &&
                StorageFolders.SequenceEqual(other.StorageFolders);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ControlEndPoints, StorageFolders);
        }
    }
}
