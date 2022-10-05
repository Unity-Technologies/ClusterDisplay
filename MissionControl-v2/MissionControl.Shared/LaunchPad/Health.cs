using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// Health diagnostic of the LaunchPad and its surrounding (changes every time it is queried).
    /// </summary>
    public class Health
    {
        /// <summary>
        /// Total CPU usage of the system (from 0.0f to 1.0f).
        /// </summary>
        public float CpuUtilization { get; set; }

        /// <summary>
        /// Number of bytes of memory currently being used on the launch pad's computer.
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Number of bytes of physical memory installed on the launch pad's computer.
        /// </summary>
        public long MemoryAvailable { get; set; }
    }
}
