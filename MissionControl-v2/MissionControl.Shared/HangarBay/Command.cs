using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// Type of <see cref="Command"/>.
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// <see cref="PrepareCommand"/>
        /// </summary>
        Prepare,
        /// <summary>
        /// <see cref="RestartCommand"/>
        /// </summary>
        Restart,
        /// <summary>
        /// <see cref="ShutdownCommand"/>
        /// </summary>
        Shutdown,
        /// <summary>
        /// <see cref="UpgradeCommand"/>
        /// </summary>
        Upgrade
    }

    /// <summary>
    /// Base class for commands sent to the HangarBay
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
