using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
        /// <see cref="ShutdownCommand"/>
        /// </summary>
        Shutdown
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
