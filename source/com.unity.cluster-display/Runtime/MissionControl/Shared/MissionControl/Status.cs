using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// State of MissionControl.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Nothing is going on, all operations are allowed.
        /// </summary>
        Idle,
        /// <summary>
        /// Preparing launch on the different launchpads.
        /// </summary>
        Preparing,
        /// <summary>
        /// launchpads have launched their payloads and everything is running.
        /// </summary>
        Launched,
        /// <summary>
        /// At least one of the launchpad failed to launch (or failed after launch).
        /// </summary>
        Failure
    }

    /// <summary>
    /// MissionControl's status.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Status: IEquatable<Status>
    {
        /// <summary>
        /// State of the LaunchPad
        /// </summary>
        public State State { get; set; } = State.Idle;

        /// <summary>
        /// When did state changed to its current value.
        /// </summary>
        public DateTime EnteredStateTime { get; set; }

        /// <summary>
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void DeepCopyFrom(Status from)
        {
            State = from.State;
            EnteredStateTime = from.EnteredStateTime;
        }

        public bool Equals(Status other)
        {
            return other != null &&
                State == other.State &&
                EnteredStateTime == other.EnteredStateTime;
        }
    }
}
