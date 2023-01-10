using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Describes a saved mission (so that it can be identified by a human).
    /// <see cref="SaveMissionCommand"/>.
    /// </summary>
    public class SavedMissionDescription: IEquatable<SavedMissionDescription>
    {
        /// <summary>
        /// Short description of the saved mission.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Details about the saved mission.
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// Creates a complete independent of from (no data should be shared between the original and the this).
        /// </summary>
        /// <param name="fromObject"><see cref="SavedMissionDescription"/> to copy from, must be same type as this.
        /// </param>
        public void DeepCopyFrom(SavedMissionDescription fromObject)
        {
            var from = fromObject;
            Name = from.Name;
            Details = from.Details;
        }

        public bool Equals(SavedMissionDescription? other)
        {
            return other != null &&
                Name == other.Name &&
                Details == other.Details;
        }
    }
}
