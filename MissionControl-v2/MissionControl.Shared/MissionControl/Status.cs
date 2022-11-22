namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// State of MissionControl.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Nothing is going on, all operations are allowed.
        /// </summary>
        Idle,
        /// <summary>
        /// Preparing launch on the different launchpads.
        /// </summary>
        /// <remarks>The only allowed operation is the <see cref="StopMissionCommand"/> to abort preparation.</remarks>
        Preparing,
        /// <summary>
        /// launchpads have launched their payloads and everything is running.
        /// </summary>
        /// <remarks>The only allowed operation is the <see cref="StopMissionCommand"/> to stop the mission.</remarks>
        Launched,
        /// <summary>
        /// At least one of the launchpad failed to launch (or failed after launch).
        /// </summary>
        /// <remarks>The only allowed operation is the <see cref="StopMissionCommand"/> to stop the mission.</remarks>
        Failure
    }

    /// <summary>
    /// MissionControl's status.
    /// </summary>
    public class Status: ObservableObject, IEquatable<Status>
    {
        /// <summary>
        /// Version number of the running MissionControl executable.
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// When did the MissionControl was started.
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

        /// <summary>
        /// State of the LaunchPad
        /// </summary>
        public State State { get; set; } = State.Idle;

        /// <summary>
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void DeepCopy(Status from)
        {
            Version = from.Version;
            StartTime = from.StartTime;
            PendingRestart = from.PendingRestart;
            StorageFolders = from.StorageFolders.Select(sfs =>
                {
                    StorageFolderStatus ret = new();
                    ret.DeepCopy(sfs);
                    return ret;
                }).ToArray();
            State = from.State;
        }

        public bool Equals(Status? other)
        {
            return other != null &&
                Version == other.Version &&
                StartTime == other.StartTime &&
                PendingRestart == other.PendingRestart &&
                StorageFolders.SequenceEqual(other.StorageFolders) &&
                State == other.State;
        }
    }
}
