// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// HangarBay's status.
    /// </summary>
    public class Status: IEquatable<Status>
    {
        /// <summary>
        /// Version number of the running HangarBay executable.
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// When did the HangarBay was started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Has some operations been done on the HangarBay that requires a restart?
        /// </summary>
        public bool PendingRestart { get; set; }

        /// <summary>
        /// Status of the different storage folders.
        /// </summary>
        public IEnumerable<StorageFolderStatus> StorageFolders { get; set; } = Enumerable.Empty<StorageFolderStatus>();

        public bool Equals(Status? other)
        {
            return other != null &&
                Version == other.Version &&
                StartTime == other.StartTime &&
                PendingRestart == other.PendingRestart &&
                StorageFolders.SequenceEqual(other.StorageFolders);
        }
    }
}
