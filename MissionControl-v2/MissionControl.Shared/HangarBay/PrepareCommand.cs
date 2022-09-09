namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to prepare a folder with the given list of payloads.
    /// </summary>
    public class PrepareCommand: Command, IEquatable<PrepareCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PrepareCommand()
        {
            Type = CommandType.Prepare;
        }

        /// <summary>
        /// Identifiers of the payloads to prepare into folder.
        /// </summary>
        public IEnumerable<Guid> PayloadIds { get; set; } = Enumerable.Empty<Guid>();

        /// <summary>
        /// URI of where to download payloads if they are not already available in the cache.
        /// </summary>
        public string PayloadSource { get; set; } = "";

        /// <summary>
        /// Path to a folder to fill with the payloads (will remove unnecessary files and copy the ones from payloadIds).
        /// </summary>
        public string Path { get; set; } = "";

        public bool Equals(PrepareCommand? other)
        {
            if (other == null || other.GetType() != typeof(PrepareCommand))
            {
                return false;
            }

            return PayloadIds.SequenceEqual(other.PayloadIds) && PayloadSource == other.PayloadSource &&
                Path == other.Path;
        }
    }
}
