using System;
using System.Text.Json;

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
        /// URI of where to download payloads if they are not already available in the HangarBay.
        /// </summary>
        public string PayloadSource { get; set; } = "";

        /// <summary>
        /// Some data (opaque to the Launchpad) to be passed using the LAUNCH_DATA environment variable both during
        /// launch and pre-launch.
        /// </summary>
        /// <remarks>Because of OS limitations the amount of data in this object should be kept reasonable (current
        /// limitation seem to be around 8192 characters).</remarks>
        public dynamic? LaunchData { get; set; }

        /// <summary>
        /// Path (relative to the toLaunch folder) to an optional executable to execute before launch.  This executable
        /// is responsible to ensure that any external dependencies are installed and ready to use.
        /// </summary>
        public string PreLaunchPath { get; set; } = "";

        /// <summary>
        /// Path (relative to the toLaunch folder) to the executable to launch when the LaunchPad receives the Launch
        /// command.
        /// </summary>
        public string LaunchPath { get; set; } = "";

        public bool Equals(PrepareCommand? other)
        {
            if (other == null || other.GetType() != typeof(PrepareCommand))
            {
                return false;
            }

            return PayloadIds.SequenceEqual(other.PayloadIds) && PayloadSource == other.PayloadSource &&
                SerializeDynamic(LaunchData) == SerializeDynamic(other.LaunchData) &&
                PreLaunchPath == other.PreLaunchPath && LaunchPath == other.LaunchPath;
        }

        static string SerializeDynamic(dynamic toSerialize)
        {
            if (!Object.ReferenceEquals(toSerialize, null))
            {
                return JsonSerializer.Serialize(toSerialize, Json.SerializerOptions);
            }
            else
            {
                return "";
            }
        }
    }
}
