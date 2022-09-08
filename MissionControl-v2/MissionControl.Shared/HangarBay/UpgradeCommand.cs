namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to restart.
    /// </summary>
    public class UpgradeCommand: Command
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public UpgradeCommand()
        {
            Type = CommandType.Upgrade;
        }

        /// <summary>
        /// URL to the zip file to download that contains the new version.
        /// </summary>
        public string NewVersionUrl { get; set; } = "";

        /// <summary>
        /// Maximum amount of time to wait for this process to exit before forcing it (killing the process).
        /// </summary>
        public int TimeoutSec { get; set; } = 60;

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(UpgradeCommand))
            {
                return false;
            }
            var other = (UpgradeCommand)obj;

            return NewVersionUrl == other.NewVersionUrl && TimeoutSec == other.TimeoutSec;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(NewVersionUrl, TimeoutSec);
        }
    }
}
