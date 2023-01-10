using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// LaunchPad's configuration.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Config: IEquatable<Config>
    {
        /// <summary>
        /// Uniquely identify this LaunchPad.
        /// </summary>
        /// <remarks>This identifier is created when first started and shall never change.</remarks>
        public Guid Identifier { get; set; } = Guid.Empty;

        /// <summary>
        /// IP address of the NIC connected to the cluster network.
        /// </summary>
        public string ClusterNetworkNic { get; set; }

        public bool Equals(Config other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                ClusterNetworkNic == other.ClusterNetworkNic;
        }
    }
}
