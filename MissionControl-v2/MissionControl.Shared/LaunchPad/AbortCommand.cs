using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> indicating to the LaunchPad that it should aborts whatever it was doing (so that its
    /// status returns to idle).
    /// </summary>
    public class AbortCommand: Command, IEquatable<AbortCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AbortCommand()
        {
            Type = CommandType.Abort;
        }

        public bool Equals(AbortCommand? other)
        {
            return other != null && other.GetType() == typeof(AbortCommand);
        }
    }
}
