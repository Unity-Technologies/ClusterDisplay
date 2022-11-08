using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Represent a LaunchParameterValue that is to be approved by capcom before proceeding to the launch.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchParameterForReview: IIncrementalCollectionObject, IEquatable<LaunchParameterForReview>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchParameterForReview(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Identifier of the object
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Identifier of the LaunchPad with which this parameter will be used for the launch.
        /// </summary>
        public Guid LaunchPadId { get; set; }

        /// <summary>
        /// <see cref="LaunchParameterValue"/> that is to be reviewed by a capcom process before launch.
        /// </summary>
        public LaunchParameterValue Value { get; set; } = new();

        /// <summary>
        /// To be set to <c>true</c> once the property value is known as valid.
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// Various comments about the changes or problems with the value if there is anything wrong.
        /// </summary>
        public string ReviewComments { get; set; } = "";

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchParameterForReview)fromObject;
            LaunchPadId = from.LaunchPadId;
            Value = from.Value.DeepClone();
            Ready = from.Ready;
            ReviewComments = from.ReviewComments;
        }

        public bool Equals(LaunchParameterForReview other)
        {
            return other != null &&
                Id == other.Id &&
                LaunchPadId == other.LaunchPadId &&
                Value.Equals(other.Value) &&
                Ready == other.Ready &&
                ReviewComments == other.ReviewComments;
        }
    }
}
