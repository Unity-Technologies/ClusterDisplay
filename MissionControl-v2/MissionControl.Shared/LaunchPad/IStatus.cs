using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    public static class IStatusExtensions
    {
        public static void CopyIStatusProperties(this IStatus to, IStatus from)
        {
            to.Version = from.Version;
            to.StartTime = from.StartTime;
            to.State = from.State;
            to.PendingRestart = from.PendingRestart;
            to.LastChanged = from.LastChanged;
        }
    }

    /// <summary>
    /// Used to share properties between <see cref="Status"/> and <see cref="LaunchPadStatus"/>.
    /// </summary>
    /// <remarks>This cannot be a base class since <see cref="LaunchPadStatus"/> would need to inherits from both this
    /// class and <see cref="IncrementalCollectionObject"/> (and <see cref="Status"/> is not an
    /// <see cref="IncrementalCollectionObject"/>).</remarks>
    public interface IStatus
    {
        /// <summary>
        /// Version number of the running LaunchPad executable.
        /// </summary>
        string Version { get; set; }

        /// <summary>
        /// When did the LaunchPad was started.
        /// </summary>
        DateTime StartTime { get; set; }

        /// <summary>
        /// State of the LaunchPad
        /// </summary>
        State State { get; set; }

        /// <summary>
        /// Has some operations been done on the LaunchPad that requires a restart?
        /// </summary>
        bool PendingRestart { get; set; }

        /// <summary>
        /// When was the last time anything changed in the current status of the LaunchPad.
        /// </summary>
        DateTime LastChanged { get; set; }
    }
}
