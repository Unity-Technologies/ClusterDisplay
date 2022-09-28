using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// State of the LaunchPad.
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Nothing is going on on the LaunchPad, could be use for new a launch.
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
        Launched
    }

    /// <summary>
    /// Status of the LaunchPad (changes once in a while).
    /// </summary>
    public class Status
    {
        /// <summary>
        /// Version number of the running LaunchPad executable.
        /// </summary>
        public string Version { get; set; } = "";

        /// <summary>
        /// When did the LaunchPad was started.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// State of the LaunchPad
        /// </summary>
        public State State { get; set; } = State.Idle;

        /// <summary>
        /// Has some operations been done on the LaunchPad that requires a restart?
        /// </summary>
        public bool PendingRestart { get; set; }

        /// <summary>
        /// When was the last time anything changed in the current status of the LaunchPad.
        /// </summary>
        public DateTime LastChanged { get; set; }

        /// <summary>
        /// Integer increased every time the status is changed.
        /// </summary>
        public ulong StatusNumber { get; set; }

        /// <summary>
        /// Fill this <see cref="Status"/> from another one.
        /// </summary>
        /// <param name="from">To fill from.</param>
        public void CopyFrom(Status from)
        {
            Version = from.Version;
            StartTime = from.StartTime;
            State = from.State;
            PendingRestart = from.PendingRestart;
            LastChanged = from.LastChanged;
            StatusNumber = from.StatusNumber;
        }
    }
}
