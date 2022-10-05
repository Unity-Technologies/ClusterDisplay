using Unity.ClusterDisplay.MissionControl.LaunchPad;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Health diagnostic of a LaunchPad and its surrounding (changes periodically).
    /// </summary>
    public class LaunchPadHealth: IncrementalCollectionObject, IEquatable<LaunchPadHealth>, IHealth
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchPadHealth(Guid id) :base(id) { }

        /// <summary>
        /// Is the status information contained in this object valid?
        /// </summary>
        /// <remarks>If false, every other properties (except <see cref="UpdateError"/> should be ignored).</remarks>
        public bool IsDefined { get; set; }

        /// <summary>
        /// Description of the error fetching a health diagnostic from the launchpad.
        /// </summary>
        public string UpdateError { get; set; } = "";

        /// <summary>
        /// When was the last time health diagnostic was fetched from the launchpad.
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// Total CPU usage of the system (from 0.0f to 1.0f).
        /// </summary>
        public float CpuUtilization { get; set; }

        /// <summary>
        /// Number of bytes of memory currently being used on the launchpad's computer.
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Number of bytes of physical memory installed on the launchpad's computer.
        /// </summary>
        public long MemoryInstalled { get; set; }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new LaunchPadHealth(Id);
        }

        public bool Equals(LaunchPadHealth? other)
        {
            if (other == null || other.GetType() != typeof(LaunchPadHealth))
            {
                return false;
            }

            return base.Equals(other) &&
                IsDefined == other.IsDefined &&
                UpdateError == other.UpdateError &&
                UpdateTime == other.UpdateTime &&
                CpuUtilization == other.CpuUtilization &&
                MemoryUsage == other.MemoryUsage &&
                MemoryInstalled == other.MemoryInstalled;
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (LaunchPadHealth)fromObject;
            IsDefined = from.IsDefined;
            UpdateError = from.UpdateError;
            UpdateTime = from.UpdateTime;
            this.CopyIHealthProperties(from);
        }
    }
}
