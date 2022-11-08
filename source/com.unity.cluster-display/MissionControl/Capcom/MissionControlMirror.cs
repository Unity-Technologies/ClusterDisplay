using System.Collections.Generic;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Stores the states of data structures mirror from MissionControl.
    /// </summary>
    /// <remarks>Only <see cref="Application"/> should modify the content of objects of this class.</remarks>
    public class MissionControlMirror
    {
        /// <summary>
        /// Last known version of <see cref="MissionControl.State"/>.
        /// </summary>
        public MissionControl.Status Status { get; set; } = new();
        /// <summary>
        /// Next version of <see cref="Status"/> to request from MissionControl.
        /// </summary>
        public ulong StatusNextVersion { get; set; }

        /// <summary>
        /// Last known version of <see cref="MissionControl.CapcomUplink"/>.
        /// </summary>
        public MissionControl.CapcomUplink CapcomUplink { get; set; } = new() {IsRunning = true};
        /// <summary>
        /// Next version of <see cref="CapcomUplink"/> to request from MissionControl.
        /// </summary>
        public ulong CapcomUplinkNextVersion { get; set; }

        /// <summary>
        /// Last known version of <see cref="MissionControl.LaunchConfiguration"/>.
        /// </summary>
        public MissionControl.LaunchConfiguration LaunchConfiguration { get; set; } = new();
        /// <summary>
        /// Next version of <see cref="LaunchConfiguration"/> to request from MissionControl.
        /// </summary>
        public ulong LaunchConfigurationNextVersion { get; set; }

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.LaunchComplex"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.LaunchComplex> Complexes { get; set; } = new();
        /// <summary>
        /// Next version of <see cref="Complexes"/> to request from MissionControl.
        /// </summary>
        public ulong ComplexesNextVersion { get; set; }

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.LaunchParameterForReview"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.LaunchParameterForReview> LaunchParametersForReview { get; set; } = new();
        /// <summary>
        /// Next version of <see cref="LaunchParametersForReview"/> to request from MissionControl.
        /// </summary>
        public ulong LaunchParametersForReviewNextVersion { get; set; }

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.Asset"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.Asset> Assets { get; set; } = new();
        /// <summary>
        /// Next version of <see cref="Assets"/> to request from MissionControl.
        /// </summary>
        public ulong AssetsNextVersion { get; set; }

        /// <summary>
        /// List of all the LaunchPads participating to the mission with all its associated information.
        /// </summary>
        /// <remarks>Updated by <see cref="ReviewLaunchParametersProcess"/>.</remarks>
        public List<LaunchPadInformation> LaunchPadsInformation { get; } = new();
    }
}
