using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// An entry in the catalog of saved missions.
    /// </summary>
    public class SavedMissionSummary: IncrementalCollectionObject, ISavedMissionSummary, IEquatable<SavedMissionSummary>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public SavedMissionSummary(Guid id) :base(id) { }

        /// <summary>
        /// Short description of the saved mission.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the saved mission.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// When was the mission saved.
        /// </summary>
        public DateTime SaveTime { get; set; }

        /// <summary>
        /// Identifier of the mission's asset.
        /// </summary>
        public Guid AssetId { get; set; }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new SavedMissionSummary(Id);
        }

        public bool Equals(SavedMissionSummary? other)
        {
            if (other == null || other.GetType() != typeof(SavedMissionSummary))
            {
                return false;
            }

            return base.Equals(other) &&
                Name == other.Name &&
                Description == other.Description &&
                SaveTime == other.SaveTime &&
                AssetId == other.AssetId;
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (SavedMissionSummary)fromObject;
            this.CopySavedMissionSummaryProperties(from);
            SaveTime = from.SaveTime;
            AssetId = from.AssetId;
        }
    }
}
