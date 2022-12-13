namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Status of a LaunchPad (changes once in a while).
    /// </summary>
    public class LaunchPadStatus: ClusterDisplay.MissionControl.LaunchPad.Status, IIncrementalCollectionObject,
        IEquatable<LaunchPadStatus>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchPadStatus(Guid id)
        {
            Id = id;
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <summary>
        /// Is the status information contained in this object valid?
        /// </summary>
        /// <remarks>If false, every other properties (except <see cref="UpdateError"/> should be ignored).</remarks>
        public bool IsDefined { get; set; }

        /// <summary>
        /// Description of the error fetching a status update from the launchpad.
        /// </summary>
        public string UpdateError { get; set; } = "";

        /// <summary>
        /// Additional status information that depends on the Launchable type running.
        /// </summary>
        public IEnumerable<LaunchPadReportDynamicEntry> DynamicEntries { get; set; } = Enumerable.Empty<LaunchPadReportDynamicEntry>();

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchPadStatus)fromObject;
            base.DeepCopyFrom(from);
            IsDefined = from.IsDefined;
            UpdateError = from.UpdateError;
            DynamicEntries = from.DynamicEntries.Select(de => de.DeepClone()).ToList();
        }

        public bool Equals(LaunchPadStatus? other)
        {
            return other != null &&
                base.Equals(other) &&
                Id == other.Id &&
                IsDefined == other.IsDefined &&
                UpdateError == other.UpdateError &&
                DynamicEntries.SequenceEqual(other.DynamicEntries);
        }
    }
}
