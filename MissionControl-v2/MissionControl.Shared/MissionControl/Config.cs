namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// MissionControl's configuration.  This data is also the content of the config.json file used to store the
    /// configuration of the service.
    /// </summary>
    public struct Config: IEquatable<Config>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public Config() { }

        /// <summary>
        /// End points to which to listen for REST commands.
        /// </summary>
        public IEnumerable<string> ControlEndPoints { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Base address that will be used by the launchpads to reach back the mission control controlling them (like
        /// to get payloads and file blobs).
        /// </summary>
        /// <remarks>Cannot use auto property because we want to store the normalized uri (https://www.unity.com ->
        /// https://www.unity.com/) so that this is the one that gets serialized to json.</remarks>
        public Uri LaunchPadsEntry {
            get => m_LaunchPadsEntry;
            set => m_LaunchPadsEntry = new Uri(value.ToString());
        }
        Uri m_LaunchPadsEntry = new Uri("http://127.0.0.1:8000/");

        /// <summary>
        /// Base address that will be used by local services to reach back mission control (like capcom launchables).
        /// </summary>
        /// <remarks>Cannot use auto property because we want to store the normalized uri (https://www.unity.com ->
        /// https://www.unity.com/) so that this is the one that gets serialized to json.</remarks>
        public Uri LocalEntry {
            get => m_LocalEntry;
            set => m_LocalEntry = new Uri(value.ToString());
        }
        Uri m_LocalEntry = new Uri("http://127.0.0.1:8000/");

        /// <summary>
        /// List of folders of where we store files (used as a cache to avoid constantly fetching from MissionControl).
        /// </summary>
        public IEnumerable<StorageFolderConfig> StorageFolders { get; set; } = Enumerable.Empty<StorageFolderConfig>();

        /// <summary>
        /// Number of seconds between health probes of the monitored resources.
        /// </summary>
        public float HealthMonitoringIntervalSec { get; set; } = 5.0f;

        /// <summary>
        /// For how long (in seconds) are we waiting for feedback from launchpads before considering them gone?
        /// </summary>
        public float LaunchPadFeedbackTimeoutSec { get; set; } = 30.0f;

        public bool Equals(Config other)
        {
            // ReSharper disable CompareOfFloatsByEqualityOperator
            return ControlEndPoints.SequenceEqual(other.ControlEndPoints) &&
                LaunchPadsEntry.Equals(other.LaunchPadsEntry) &&
                LocalEntry.Equals(other.LocalEntry) &&
                StorageFolders.SequenceEqual(other.StorageFolders) &&
                HealthMonitoringIntervalSec == other.HealthMonitoringIntervalSec &&
                LaunchPadFeedbackTimeoutSec == other.LaunchPadFeedbackTimeoutSec;
            // ReSharper restore CompareOfFloatsByEqualityOperator
        }
    }
}
