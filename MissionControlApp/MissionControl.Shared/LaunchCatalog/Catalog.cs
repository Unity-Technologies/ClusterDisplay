using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Root of the LaunchCatalog.json that describe something that can be launched with MissionControl.
    /// </summary>
    public class Catalog
    {
        /// <summary>
        /// List of all the payloads shared by the different <see cref="Launchable"/>s.
        /// </summary>
        public IEnumerable<Payload> Payloads { get; set; } = Enumerable.Empty<Payload>();

        /// <summary>
        /// List of all the things that can be launched on different launchpads of mission control.
        /// </summary>
        public IEnumerable<Launchable> Launchables { get; set; } = Enumerable.Empty<Launchable>();
    }
}
