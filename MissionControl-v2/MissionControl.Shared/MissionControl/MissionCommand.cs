using System;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Type of <see cref="MissionCommand"/>.
    /// </summary>
    public enum MissionCommandType
    {
        /// <summary>
        /// <see cref="SaveMissionCommand"/>
        /// </summary>
        Save,
        /// <summary>
        /// <see cref="LoadMissionCommand"/>
        /// </summary>
        Load,
        /// <summary>
        /// <see cref="LaunchMissionCommand"/>
        /// </summary>
        Launch,
        /// <summary>
        /// <see cref="StopMissionCommand"/>
        /// </summary>
        Stop
    }

    /// <summary>
    /// Base class for commands sent to MissionControl
    /// </summary>
    [JsonConverter(typeof(MissionCommandJsonConverter))]
    public abstract class MissionCommand
    {
        /// <summary>
        /// Type of command.
        /// </summary>
        // ReSharper disable once MemberCanBeProtected.Global -> Need to be public for Json serialization
        // ReSharper disable once UnusedAutoPropertyAccessor.Global -> Used by Json serialization
        public MissionCommandType Type { get; protected set; }
    }
}
