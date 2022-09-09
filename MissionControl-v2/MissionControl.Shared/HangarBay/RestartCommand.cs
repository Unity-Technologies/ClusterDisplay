namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to restart.
    /// </summary>
    public class RestartCommand: Command, IEquatable<RestartCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RestartCommand()
        {
            Type = CommandType.Restart;
        }

        /// <summary>
        /// Maximum amount of time to wait for this process to exit before forcing it (killing the process).
        /// </summary>
        public int TimeoutSec { get; set; } = 60;

        public bool Equals(RestartCommand? other)
        {
            if (other == null || other.GetType() != typeof(RestartCommand))
            {
                return false;
            }

            return TimeoutSec == other.TimeoutSec;
        }
    }
}
