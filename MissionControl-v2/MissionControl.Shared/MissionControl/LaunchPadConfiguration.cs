using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Configuration of a <see cref="LaunchPad"/> for a mission.
    /// </summary>
    /// <remarks>Parameters property is still to be implemented</remarks>
    public class LaunchPadConfiguration: IEquatable<LaunchPadConfiguration>
    {
        /// <summary>
        /// <see cref="LaunchPad"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Name of the <see cref="Launchable"/> to launch on this launchpad.
        /// </summary>
        public string LaunchableName { get; set; } = "";

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        /// <returns></returns>
        public LaunchPadConfiguration DeepClone()
        {
            LaunchPadConfiguration ret = new();
            ret.Identifier = Identifier;
            ret.LaunchableName = LaunchableName;
            return ret;
        }

        public bool Equals(LaunchPadConfiguration? other)
        {
            if (other == null || other.GetType() != typeof(LaunchPadConfiguration))
            {
                return false;
            }

            return Identifier == other.Identifier &&
                LaunchableName == other.LaunchableName;
        }
    }
}
