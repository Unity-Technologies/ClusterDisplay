using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// State of the LaunchPad.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Nothing is going on the LaunchPad, could be use for new a launch.
        /// </summary>
        Idle,
        /// <summary>
        /// Currently receiving a payload to launch from the HangarBay.
        /// </summary>
        GettingPayload,
        /// <summary>
        /// Executing pre-launch executable to prepare the launchpad to the payload.
        /// </summary>
        PreLaunch,
        /// <summary>
        /// Everything is ready, waiting for launch signal (to have a coordinated launch on all LaunchPads).
        /// </summary>
        WaitingForLaunch,
        /// <summary>
        /// Payload is launched (and still in the air (process running) otherwise state would have been changed to
        /// idle).
        /// </summary>
        Launched,
        /// <summary>
        /// Mission is finished (either with success or failure).
        /// </summary>
        Over
    }

    /// <summary>
    /// Status of the LaunchPad (changes once in a while).
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
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        // ReSharper disable once MemberCanBeProtected.Global
        public void DeepCopyFrom(Status from)
        {
            State = from.State;
        }

        public bool Equals(Status other)
        {
            return other != null &&
                State == other.State;
        }
    }
}
