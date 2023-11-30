namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Gives the list of allowed values for a <see cref="LaunchParameter"/>.
    /// </summary>
    public class ListConstraint: Constraint, IEquatable<ListConstraint>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ListConstraint()
        {
            Type = ConstraintType.List;
        }

        /// <summary>
        /// List of choices
        /// </summary>
        public IEnumerable<string> Choices { get; set; } = Enumerable.Empty<string>();

        /// <inheritdoc/>
        public override bool Validate(object value)
        {
            return Choices.Contains(value);
        }

        /// <inheritdoc/>
        public override Constraint DeepClone()
        {
            ListConstraint ret = new();
            ret.Choices = Choices.ToList();
            return ret;
        }

        public bool Equals(ListConstraint? other)
        {
            return other != null && 
                Choices.SequenceEqual(other.Choices);
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((ListConstraint)other);
        }
    }
}
