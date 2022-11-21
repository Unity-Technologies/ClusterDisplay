namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Health diagnostic of a LaunchPad and its surrounding (changes periodically).
    /// </summary>
    public class LaunchPadHealth: ClusterDisplay.MissionControl.LaunchPad.Health, IIncrementalCollectionObject,
        IEquatable<LaunchPadHealth>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchPadHealth(Guid id)
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
        /// Description of the error fetching a health diagnostic from the launchpad.
        /// </summary>
        public string UpdateError { get; set; } = "";

        /// <summary>
        /// When was the last time health diagnostic was fetched from the launchpad.
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchPadHealth)fromObject;
            base.DeepCopyFrom(from);
            IsDefined = from.IsDefined;
            UpdateError = from.UpdateError;
            UpdateTime = from.UpdateTime;
        }

        public bool Equals(LaunchPadHealth? other)
        {
            return other != null &&
                base.Equals(other) &&
                Id == other.Id &&
                IsDefined == other.IsDefined &&
                UpdateError == other.UpdateError &&
                UpdateTime == other.UpdateTime;
        }
    }
}
