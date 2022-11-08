using System;
using System.Text.Json.Nodes;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> indicating that the LaunchPad should prepare for launching the specified payload.
    /// </summary>
    public class PrepareCommand: Command, IEquatable<PrepareCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PrepareCommand()
        {
            Type = CommandType.Prepare;
        }

        /// <summary>
        /// Identifiers of the payloads to prepare for launch.
        /// </summary>
        public IEnumerable<Guid> PayloadIds { get; set; } = Enumerable.Empty<Guid>();

        /// <summary>
        /// URI to the mission control asking to prepare a launch.
        /// </summary>
        /// <remarks>It will be used to download the payloads and to set the MISSIONCONTROL_ENTRY environment variable
        /// that can be used by payloads to monitor Mission Control.<br/><br/>
        /// Cannot use auto property because we want to store the normalized uri so that this is the one that
        /// gets serialized to json.</remarks>
        public Uri? MissionControlEntry {
            get => m_MissionControlEntry;
            set => m_MissionControlEntry = value != null ? new Uri(value.ToString()) : null;
        }
        Uri? m_MissionControlEntry;

        /// <summary>
        /// Some data (opaque to all parts of MissionControl, only to be used by the launch and pre-launch executables)
        /// to be passed using the LAUNCHABLE_DATA environment variable both during launch and pre-launch.  This is the
        /// same hard-coded data for all nodes of the cluster, useful for configuring some options decided at the moment
        /// of producing the launch catalog.
        /// </summary>
        public JsonNode? LaunchableData { get; set; }

        /// <summary>
        /// Some data (opaque to the Launchpad) to be passed using the LAUNCH_DATA environment variable both during
        /// launch and pre-launch.
        /// </summary>
        /// <remarks>Because of OS limitations the amount of data in this object should be kept reasonable (current
        /// limitation seem to be around 8192 characters).</remarks>
        public JsonNode? LaunchData { get; set; }

        /// <summary>
        /// Path (relative to where prepared payload is stored) to an optional executable to execute before launch.
        /// This executable is responsible to ensure that any external dependencies are installed and ready to use.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string PreLaunchPath { get; set; } = "";

        /// <summary>
        /// Path (relative to where prepared payload is stored) to the executable to launch when the LaunchPad receives
        /// the Launch command.
        /// </summary>
        /// <remarks>Can be an executable, a ps1 or an assemblyrun:// url.</remarks>
        public string LaunchPath { get; set; } = "";

        public bool Equals(PrepareCommand? other)
        {
            if (other == null)
            {
                return false;
            }

            if ((MissionControlEntry == null) != (other.MissionControlEntry == null))
            {
                return false;
            }

            return PayloadIds.SequenceEqual(other.PayloadIds) &&
                (MissionControlEntry == null || MissionControlEntry.Equals(other.MissionControlEntry)) &&
                SerializeJsonNode(LaunchableData) == SerializeJsonNode(other.LaunchableData) &&
                SerializeJsonNode(LaunchData) == SerializeJsonNode(other.LaunchData) &&
                PreLaunchPath == other.PreLaunchPath &&
                LaunchPath == other.LaunchPath;
        }

        static string SerializeJsonNode(JsonNode? toSerialize)
        {
            return toSerialize != null ? toSerialize.ToJsonString() : "";
        }
    }
}
