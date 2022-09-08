namespace Unity.ClusterDisplay.MissionControl.HangarBay
{
    /// <summary>
    /// <see cref="Command"/> asking the HangarBay to prepare a folder with the given list of payloads.
    /// </summary>
    public class PrepareCommand: Command
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

        public override bool Equals(Object? obj)
        {
            if (obj == null || obj.GetType() != typeof(PrepareCommand))
            {
                return false;
            }
            var other = (PrepareCommand)obj;

            return PayloadIds.SequenceEqual(other.PayloadIds) && PayloadSource == other.PayloadSource &&
                Path == other.Path;
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new();
            foreach (var payloadId in PayloadIds)
            {
                hashCode.Add(payloadId.GetHashCode());
            }
            return HashCode.Combine(hashCode.ToHashCode(), PayloadSource, Path);
        }
    }
}
