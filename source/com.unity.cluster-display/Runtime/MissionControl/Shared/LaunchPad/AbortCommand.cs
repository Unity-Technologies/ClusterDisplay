namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// Command indicating to the LaunchPad that it should aborts whatever it was doing (so that its status returns to
    /// idle).
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class AbortCommand
    {
        /// <summary>
        /// Type of the command.
        /// </summary>
        /// <remarks>We only need to serialize <see cref="AbortCommand"/>, so we don't need to real enum, with json
        /// converters, ...</remarks>
        public string Type { get; set; } = "abort";

        /// <summary>
        /// Resulting state of the abort command will be over instead of idle.
        /// </summary>
        /// <remarks>Useful to get in the same state as if it would be the payload that exited by itself.</remarks>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public bool AbortToOver { get; set; }
    }
}
