using System;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Configuration of a <see cref="LaunchPad"/> for a mission.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchPadConfiguration: IEquatable<LaunchPadConfiguration>
    {
        /// <summary>
        /// <see cref="LaunchPad"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; } = Guid.Empty;

        /// <summary>
        /// Name of the <see cref="Launchable"/> to launch on this launchpad.
        /// </summary>
        public string LaunchableName { get; set; } = "";

        public bool Equals(LaunchPadConfiguration other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                LaunchableName == other.LaunchableName;
        }
    }
}
