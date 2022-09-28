namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> asking the LaunchPad to upgrade.
    /// </summary>
    public class UpgradeCommand: Command, IEquatable<UpgradeCommand>
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

        public bool Equals(UpgradeCommand? other)
        {
            if (other == null || other.GetType() != typeof(UpgradeCommand))
            {
                return false;
            }

            return NewVersionUrl == other.NewVersionUrl && TimeoutSec == other.TimeoutSec;
        }
    }
}
