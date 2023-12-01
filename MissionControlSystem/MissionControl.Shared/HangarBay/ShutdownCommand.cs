namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to shutdown.  Use with care as the only way to restart it is to do 
    /// some manual interventions on the computer running it, designed to be sued as part of automated testing.
    /// </summary>
    public class ShutdownCommand: Command, IEquatable<ShutdownCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ShutdownCommand()
        {
            Type = CommandType.Shutdown;
        }

        public bool Equals(ShutdownCommand? other)
        {
            return other != null && other.GetType() == typeof(ShutdownCommand);
        }
    }
}
