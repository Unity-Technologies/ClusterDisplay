using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Command to load a saved mission as the current mission.
    /// </summary>
    public class LoadMissionCommand: MissionCommand, IEquatable<LoadMissionCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LoadMissionCommand()
        {
            Type = MissionCommandType.Load;
        }

        /// <summary>
        /// Identifier of the saved mission to load.
        /// </summary>
        public Guid Identifier { get; set; }

        public bool Equals(LoadMissionCommand? other)
        {
            return other != null &&
                Identifier == other.Identifier;
        }
    }
}
