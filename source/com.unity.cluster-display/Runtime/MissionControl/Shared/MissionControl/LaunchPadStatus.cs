using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Status of a LaunchPad (changes once in a while).
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
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

        /// <summary>
        /// Identifier of the object
        /// </summary>
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

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchPadStatus)fromObject;
            base.DeepCopyFrom(from);
            IsDefined = from.IsDefined;
            UpdateError = from.UpdateError;
        }

        public bool Equals(LaunchPadStatus other)
        {
            return other != null &&
                base.Equals(other) &&
                Id == other.Id &&
                IsDefined == other.IsDefined &&
                UpdateError == other.UpdateError;
        }
    }
}
