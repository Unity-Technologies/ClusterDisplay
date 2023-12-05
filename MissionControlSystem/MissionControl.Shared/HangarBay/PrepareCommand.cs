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
        /// <remarks>Cannot use auto property because we want to store the normalized uri so that this is the one that
        /// gets serialized to json.</remarks>
        public Uri? PayloadSource {
            get => m_PayloadSource;
            set => m_PayloadSource = value != null ? new Uri(value.ToString()) : null;
        }
        Uri? m_PayloadSource;

        /// <summary>
        /// Path to a folder to fill with the payloads (will remove unnecessary files and copy the ones from payloadIds).
        /// </summary>
        public string Path { get; set; } = "";

        public bool Equals(PrepareCommand? other)
        {
            if (other == null)
            {
                return false;
            }

            if ((PayloadSource == null) != (other.PayloadSource == null))
            {
                return false;
            }

            return PayloadIds.SequenceEqual(other.PayloadIds) &&
                (PayloadSource == null || PayloadSource.Equals(other.PayloadSource)) &&
                Path == other.Path;
        }
    }
}
