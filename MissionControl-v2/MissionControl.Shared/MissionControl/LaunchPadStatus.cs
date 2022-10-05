using Unity.ClusterDisplay.MissionControl.LaunchPad;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Status of a LaunchPad (changes once in a while).
    /// </summary>
    public class LaunchPadStatus: IncrementalCollectionObject, IEquatable<LaunchPadStatus>, IStatus
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchPadStatus(Guid id) :base(id) { }

        /// <summary>
        /// Is the status information contained in this object valid?
        /// </summary>
        /// <remarks>If false, every other properties (except <see cref="UpdateError"/> should be ignored).</remarks>
        public bool IsDefined { get; set; }

        /// <summary>
        /// Description of the error fetching a status update from the launchpad.
        /// </summary>
        public string UpdateError { get; set; } = "";

        /// <summary>
        /// Version number of the running LaunchPad executable.
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// When did the LaunchPad was started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// State of the LaunchPad
        /// </summary>
        public ClusterDisplay.MissionControl.LaunchPad.State State { get; set; } =
            ClusterDisplay.MissionControl.LaunchPad.State.Idle;

        /// <summary>
        /// Has some operations been done on the LaunchPad that requires a restart?
        /// </summary>
        public bool PendingRestart { get; set; }

        /// <summary>
        /// When was the last time anything changed in the current status of the LaunchPad.
        /// </summary>
        public DateTime LastChanged { get; set; }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new LaunchPadStatus(Id);
        }

        public bool Equals(LaunchPadStatus? other)
        {
            if (other == null || other.GetType() != typeof(LaunchPadStatus))
            {
                return false;
            }

            return base.Equals(other) &&
                IsDefined == other.IsDefined &&
                UpdateError == other.UpdateError &&
                Version == other.Version &&
                StartTime == other.StartTime &&
                State == other.State &&
                PendingRestart == other.PendingRestart &&
                LastChanged == other.LastChanged;
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (LaunchPadStatus)fromObject;
            IsDefined = from.IsDefined;
            UpdateError = from.UpdateError;
            this.CopyIStatusProperties(from);
        }
    }
}
