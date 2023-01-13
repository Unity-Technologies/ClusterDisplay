using System.Collections.Generic;
using System.Net.Http;

namespace Unity.ClusterDisplay.MissionControl.Capcom
{
    /// <summary>
    /// Stores the states of data structures mirror from MissionControl.
    /// </summary>
    /// <remarks>Only <see cref="Application"/> should modify the content of objects of this class.</remarks>
    public class MissionControlMirror
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="missionControlHttpClient"><see cref="HttpClient"/> configured to send request to
        /// MissionControl.</param>
        public MissionControlMirror(HttpClient missionControlHttpClient)
        {
            MissionControlHttpClient = missionControlHttpClient;
        }

        /// <summary>
        /// <see cref="HttpClient"/> configured to send request to MissionControl.
        /// </summary>
        public HttpClient MissionControlHttpClient { get; }

        /// <summary>
        /// Last known version of <see cref="MissionControl.State"/>.
        /// </summary>
        public MissionControl.Status Status { get; set; } = new();
        /// <summary>
        /// Version number that gets incremented every time <see cref="Status"/> changes.
        /// </summary>
        public ulong StatusVersionNumber { get; set; }

        /// <summary>
        /// Last known version of <see cref="MissionControl.CapcomUplink"/>.
        /// </summary>
        public MissionControl.CapcomUplink CapcomUplink { get; set; } = new() {IsRunning = true};
        /// <summary>
        /// Version number that gets incremented every time <see cref="CapcomUplink"/> changes.
        /// </summary>
        public ulong CapcomUplinkVersionNumber { get; set; }

        /// <summary>
        /// Last known version of <see cref="MissionControl.LaunchConfiguration"/>.
        /// </summary>
        public MissionControl.LaunchConfiguration LaunchConfiguration { get; set; } = new();
        /// <summary>
        /// Version number that gets incremented every time <see cref="LaunchConfiguration"/> changes.
        /// </summary>
        public ulong LaunchConfigurationVersionNumber { get; set; }

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.LaunchComplex"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.LaunchComplex> Complexes { get; set; } = new();

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.LaunchParameterForReview"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.LaunchParameterForReview> LaunchParametersForReview { get; set; } = new();

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.Asset"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.Asset> Assets { get; set; } = new();

        /// <summary>
        /// Last known version of MissionControl's collection of <see cref="MissionControl.LaunchPadStatus"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.LaunchPadStatus> LaunchPadsStatus { get; set; } = new();

        /// <summary>
        /// Last known version of MissionControl's desired collection of
        /// <see cref="MissionControl.MissionParameterValue"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.MissionParameterValue> ParametersDesiredValues { get; set; } = new();

        /// <summary>
        /// Last known version of MissionControl's effective collection of
        /// <see cref="MissionControl.MissionParameterValue"/>.
        /// </summary>
        public IncrementalCollection<MissionControl.MissionParameterValue> ParametersEffectiveValues { get; set; } = new();

        /// <summary>
        /// List of all the LaunchPads participating to the mission with all its associated information.
        /// </summary>
        /// <remarks>Updated by <see cref="ReviewLaunchParametersProcess"/>.</remarks>
        public List<LaunchPadInformation> LaunchPadsInformation { get; } = new();
        /// <summary>
        /// Gets incremented every time LaunchPadsInformation is filled with new LaunchPadInformation -> Every time
        /// Mission Control launches a new mission.
        /// </summary>
        public ulong LaunchPadsInformationVersion { get; set; }
    }

    /// <summary>
    /// Stores the next update versions for each incremental members of a <see cref="MissionControlMirror"/>.
    /// </summary>
    /// <remarks>Not stored in <see cref="MissionControlMirror"/> since this is something internal to
    /// <see cref="Application"/> that does not need to be accessible to every <see cref="IApplicationProcess"/>.
    /// </remarks>
    class MissionControlMirrorNextUpdates
    {
        /// <summary>
        /// Next version of <see cref="MissionControlMirror.Status"/> to request from MissionControl.
        /// </summary>
        public ulong StatusNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.CapcomUplink"/> to request from MissionControl.
        /// </summary>
        public ulong CapcomUplinkNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.LaunchConfiguration"/> to request from MissionControl.
        /// </summary>
        public ulong LaunchConfigurationNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.Complexes"/> to request from MissionControl.
        /// </summary>
        public ulong ComplexesNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.LaunchParametersForReview"/> to request from MissionControl.
        /// </summary>
        public ulong LaunchParametersForReviewNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.Assets"/> to request from MissionControl.
        /// </summary>
        public ulong AssetsNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.LaunchPadsStatus"/> to request from MissionControl.
        /// </summary>
        public ulong LaunchPadsStatusNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.ParametersDesiredValues"/> to request from MissionControl.
        /// </summary>
        public ulong ParametersDesiredValuesNextVersion { get; set; }

        /// <summary>
        /// Next version of <see cref="MissionControlMirror.ParametersEffectiveValues"/> to request from MissionControl.
        /// </summary>
        public ulong ParametersEffectiveValuesNextVersion { get; set; }
    }
}
