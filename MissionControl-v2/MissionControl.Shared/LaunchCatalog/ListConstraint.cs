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

        public bool Equals(ListConstraint? other)
        {
            return other != null && other.GetType() == typeof(ListConstraint) &&
                Choices.SequenceEqual(other.Choices);
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((ListConstraint)other);
        }
    }
}
