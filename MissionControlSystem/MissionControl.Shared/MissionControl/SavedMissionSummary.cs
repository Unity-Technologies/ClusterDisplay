using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// An entry in the catalog of saved missions.
    /// </summary>
    public class SavedMissionSummary: IIncrementalCollectionObject, IEquatable<SavedMissionSummary>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public SavedMissionSummary(Guid id)
        {
            Id = id;
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <summary>
        /// Describes a saved mission (so that it can be identified by a human).
        /// </summary>
        public SavedMissionDescription Description { get; set; } = new();

        /// <summary>
        /// When was the mission saved.
        /// </summary>
        public DateTime SaveTime { get; set; }

        /// <summary>
        /// Identifier of the mission's asset.
        /// </summary>
        public Guid AssetId { get; set; }

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (SavedMissionSummary)fromObject;
            Description.DeepCopyFrom(from.Description);
            SaveTime = from.SaveTime;
            AssetId = from.AssetId;
        }

        public bool Equals(SavedMissionSummary? other)
        {
            return other != null &&
                Id == other.Id &&
                Description.Equals(other.Description) &&
                SaveTime == other.SaveTime &&
                AssetId == other.AssetId;
        }
    }
}
