namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Type of a <see cref="MissionParameter"/>.
    /// </summary>
    /// <remarks>This list of types will be extended to include more complex types such as color, vector3, ...</remarks>
    public enum MissionParameterType
    {
        /// <summary>
        /// Boolean value.
        /// </summary>
        /// <remarks>Stored as json <c>true</c> or <c>false</c>.</remarks>
        Boolean,
        /// <summary>
        /// Command value.
        /// </summary>
        /// <remarks>Stored as a string that contains a uuid.  Setting the desired value to a new uuid indicates that
        /// the command is to be executed.  Setting it twice in quick succession before the effective value is updated
        /// will cause the command to be executed only once.  A null effective value indicate that the command was
        /// never ran.</remarks>
        Command,
        /// <summary>
        /// Integer number value.
        /// </summary>
        /// <remarks>Stored as json integer value.</remarks>
        Integer,
        /// <summary>
        /// Floating point number value.
        /// </summary>
        /// <remarks>Stored as json numerical value.</remarks>
        Float,
        /// <summary>
        /// String value.
        /// </summary>
        /// <remarks>Stored as json string value.</remarks>
        String
    }

    /// <summary>
    /// Describes a parameter for which the value can be controlled at runtime.
    /// </summary>
    public class MissionParameter: IIncrementalCollectionObject, IWithMissionParameterValueIdentifier,
        IEquatable<MissionParameter>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public MissionParameter(Guid id)
        {
            Id = id;
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <summary>
        /// Case sensitive string used to find the corresponding <see cref="MissionParameterValue"/>.
        /// </summary>
        /// <remarks>Should be set such as that if two different assets expose an equivalent
        /// <see cref="MissionParameter"/>, the valueIdentifier will match between the two assets.  This is the main
        /// reason we are not relying on <see cref="IIncrementalCollectionObject.Id"/> for that purpose as it would be
        /// challenging to have matching uuids.<br/><br/>
        /// No two <see cref="MissionParameter"/> should have the same valueIdentifier within currentMission/parameters.
        /// </remarks>
        public string ValueIdentifier { get; set; } = "";

        /// <summary>
        /// Name of the parameter as displayed to the user.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the parameter (that could for example be displayed in a tooltip).
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Type of parameter.
        /// </summary>
        public MissionParameterType Type { get; set; }

        /// <summary>
        /// Constraint on values of the parameter.
        /// </summary>
        public Constraint? Constraint { get; set; }

        /// <summary>
        /// Name of the group (when displaying the parameter to allow to see or set its value) this parameter is a part
        /// of.  Nested groups can be expressed by separating them with a slash (/).
        /// </summary>
        /// <remarks>The first nested group (start of the group string before the first slash (/) if any is present) can
        /// be the identifier of a LaunchPad, indicating this parameter is only affecting payloads launched by this
        /// launchpad.</remarks>
        public string Group { get; set; } = "";

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (MissionParameter)fromObject;
            ValueIdentifier = from.ValueIdentifier;
            Name = from.Name;
            Description = from.Description;
            Type = from.Type;
            Constraint = from.Constraint?.DeepClone();
            Group = from.Group;
        }

        public bool Equals(MissionParameter? other)
        {
            if (other == null || (Constraint == null) != (other.Constraint == null))
            {
                return false;
            }

            return Id == other.Id &&
                ValueIdentifier == other.ValueIdentifier &&
                Name == other.Name &&
                Description == other.Description &&
                Type == other.Type &&
                (other.Constraint == null || other.Constraint.Equals(Constraint)) &&
                Group == other.Group;
        }
    }
}
