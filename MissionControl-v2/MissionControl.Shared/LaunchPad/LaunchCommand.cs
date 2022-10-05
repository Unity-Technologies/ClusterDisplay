using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> indicating the LaunchPad it should launch the payload.
    /// </summary>
    public class LaunchCommand: Command, IEquatable<LaunchCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LaunchCommand()
        {
            Type = CommandType.Launch;
        }

        public bool Equals(LaunchCommand? other)
        {
            return other != null && other.GetType() == typeof(LaunchCommand);
        }
    }
}
