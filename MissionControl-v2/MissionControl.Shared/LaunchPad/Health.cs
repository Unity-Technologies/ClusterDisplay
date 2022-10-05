using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public static class HealthExtensions
    {
        public static void CopyIHealthProperties(this IHealth to, IHealth from)
        {
            to.CpuUtilization = from.CpuUtilization;
            to.MemoryUsage = from.MemoryUsage;
            to.MemoryInstalled = from.MemoryInstalled;
        }
    }

    /// <summary>
    /// Health diagnostic of the LaunchPad and its surrounding (changes every time it is queried).
    /// </summary>
    public class Health: IHealth
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
