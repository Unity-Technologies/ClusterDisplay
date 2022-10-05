using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    public static class SavedMissionSummaryExtensions
    {
        public static void CopySavedMissionSummaryProperties(this ISavedMissionSummary to, ISavedMissionSummary from)
        {
            to.Name = from.Name;
            to.Description = from.Description;
        }
    }

    /// <summary>
    /// Used to have some common properties between <see cref="SavedMissionSummary"/> and
    /// <see cref="SaveMissionCommand"/>.
    /// </summary>
    /// <remarks>This cannot be a base class since <see cref="SavedMissionSummary"/> would need to inherits from both
    /// this class and <see cref="IncrementalCollectionObject"/> (and <see cref="SaveMissionCommand"/> is not an
    /// <see cref="IncrementalCollectionObject"/>).</remarks>
    public interface ISavedMissionSummary
    {
        /// <summary>
        /// Short description of the saved mission.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Detailed description of the saved mission.
        /// </summary>
        public string Description { get; set; }
    }
}
