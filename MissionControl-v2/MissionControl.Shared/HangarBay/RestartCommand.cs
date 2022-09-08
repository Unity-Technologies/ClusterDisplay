namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to restart.
    /// </summary>
    public class RestartCommand: Command
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

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(RestartCommand))
            {
                return false;
            }
            var other = (RestartCommand)obj;

            return TimeoutSec == other.TimeoutSec;
        }

        public override int GetHashCode()
        {
            return TimeoutSec.GetHashCode();
        }
    }
}
