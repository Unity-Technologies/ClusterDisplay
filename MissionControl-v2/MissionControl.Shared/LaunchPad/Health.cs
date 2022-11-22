using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// Health diagnostic of the LaunchPad and its surrounding (changes every time it is queried).
    /// </summary>
    public class Health: IEquatable<Health>
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

        /// <summary>
        /// Fill this <see cref="Health"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void DeepCopyFrom(Health from)
        {
            CpuUtilization = from.CpuUtilization;
            MemoryUsage = from.MemoryUsage;
            MemoryInstalled = from.MemoryInstalled;
        }

        public bool Equals(Health? other)
        {
            return other != null &&
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                CpuUtilization == other.CpuUtilization &&
                MemoryUsage == other.MemoryUsage &&
                MemoryInstalled == other.MemoryInstalled;
        }
    }
}
