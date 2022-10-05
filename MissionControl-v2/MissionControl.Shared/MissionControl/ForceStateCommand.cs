using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// <see cref="Command"/> asking the LaunchPad to for its state to the specified one (regardless of the current
    /// "real state").
    /// </summary>
    /// <remarks>This command is strictly designed for unit testing and has no other purpose outside that scope.</remarks>
    public class ForceStateCommand: Command, IEquatable<ForceStateCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ForceStateCommand()
        {
            Type = CommandType.ForceState;
        }

        /// <summary>
        /// The state into which to force MissionControl.
        /// </summary>
        public State State { get; set; }

        /// <summary>
        /// Do we also keep the status locked until <see cref="ControlFile"/> is gone?
        /// </summary>
        public bool KeepLocked { get; set; }

        /// <summary>
        /// State will be forced for as long as this file exists
        /// </summary>
        public string ControlFile { get; set; } = "";

        public bool Equals(ForceStateCommand? other)
        {
            if (other == null || other.GetType() != typeof(ForceStateCommand))
            {
                return false;
            }

            return State == other.State &&
                KeepLocked == other.KeepLocked &&
                ControlFile == other.ControlFile;
        }
    }
}
