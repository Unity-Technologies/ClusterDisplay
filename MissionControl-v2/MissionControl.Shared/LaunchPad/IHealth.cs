using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// Used to share properties between <see cref="Health"/> and <see cref="LaunchPadHealth"/>.
    /// </summary>
    /// <remarks>This cannot be a base class since <see cref="LaunchPadHealth"/> would need to inherits from both this
    /// class and <see cref="IncrementalCollectionObject"/> (and <see cref="Status"/> is not an
    /// <see cref="IncrementalCollectionObject"/>).</remarks>
    public interface IHealth
    {
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
    }
}
