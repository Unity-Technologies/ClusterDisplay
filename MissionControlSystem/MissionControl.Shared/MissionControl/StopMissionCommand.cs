using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Command to stop the current mission.
    /// </summary>
    public class StopMissionCommand: MissionCommand, IEquatable<StopMissionCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public StopMissionCommand()
        {
            Type = MissionCommandType.Stop;
        }

        public bool Equals(StopMissionCommand? other)
        {
            return other != null && other.GetType() == typeof(StopMissionCommand);
        }
    }
}
