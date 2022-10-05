namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Command to launch the current mission.
    /// </summary>
    public class LaunchMissionCommand: MissionCommand, IEquatable<LaunchMissionCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LaunchMissionCommand()
        {
            Type = MissionCommandType.Launch;
        }

        public bool Equals(LaunchMissionCommand? other)
        {
            return other != null && other.GetType() == typeof(LaunchMissionCommand);
        }
    }
}
