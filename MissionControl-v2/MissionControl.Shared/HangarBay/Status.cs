namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    public class Status
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

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(Status))
            {
                return false;
            }
            var other = (Status)obj;

            return Version == other.Version && StartTime == other.StartTime &&
                PendingRestart == other.PendingRestart && StorageFolders.SequenceEqual(other.StorageFolders);
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new();
            foreach (var storageFolder in StorageFolders)
            {
                hashCode.Add(storageFolder.GetHashCode());
            }
            return HashCode.Combine(Version, StartTime, PendingRestart, hashCode.ToHashCode());
        }
    }
}
