using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Type of <see cref="Command"/>.
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// <see cref="RestartCommand"/>
        /// </summary>
        Restart,
        /// <summary>
        /// <see cref="ShutdownCommand"/>
        /// </summary>
        Shutdown,
        /// <summary>
        /// <see cref="ForceStateCommand"/>
        /// </summary>
        ForceState
    }

    /// <summary>
    /// Base class for commands sent to MissionControl
    /// </summary>
    [JsonConverter(typeof(CommandJsonConverter))]
    public abstract class Command
    {
        /// <summary>
        /// Type of command.
        /// </summary>
        public CommandType Type { get; protected set; }
    }
}
