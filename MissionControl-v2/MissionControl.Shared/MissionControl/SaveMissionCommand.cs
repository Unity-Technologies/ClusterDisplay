using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Command indicating to mission control that it should save the current mission's definition.
    /// </summary>
    public class SaveMissionCommand: MissionCommand, IEquatable<SaveMissionCommand>
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
        /// Describes a saved mission (so that it can be identified by a human).
        /// </summary>
        public SavedMissionDescription Description { get; set; } = new();

        public bool Equals(SaveMissionCommand? other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                Description.Equals(other.Description);
        }
    }
}
