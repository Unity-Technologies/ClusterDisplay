using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Command indicating to mission control that it should save the current mission's definition.
    /// </summary>
    public class SaveMissionCommand: MissionCommand, ISavedMissionSummary, IEquatable<SaveMissionCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SaveMissionCommand()
        {
            Type = MissionCommandType.Save;
        }

        /// <summary>
        /// Identify the saved mission to override with this one.
        /// </summary>
        /// <remarks>Omit (<see cref="Guid.Empty"/>) to save as a new mission.</remarks>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Short description of the mission to save.
        /// </summary>
        /// <remarks>No validation is done on the name and two different missions can have the same name.</remarks>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the mission to save.
        /// </summary>
        public string Description { get; set; } = "";

        public bool Equals(SaveMissionCommand? other)
        {
            if (other == null || other.GetType() != typeof(SaveMissionCommand))
            {
                return false;
            }

            return Identifier == other.Identifier &&
                Name == other.Name &&
                Description == other.Description;
        }
    }
}
