using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> indicating to the LaunchPad to clear any previous file waiting for a potential relaunch
    /// on the LaunchPad.
    /// </summary>
    public class ClearCommand: Command, IEquatable<ClearCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ClearCommand()
        {
            Type = CommandType.Clear;
        }

        public bool Equals(ClearCommand? other)
        {
            return other != null && other.GetType() == typeof(ClearCommand);
        }
    }
}
