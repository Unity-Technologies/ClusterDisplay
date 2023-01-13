using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Gives the list of allowed values for a <see cref="LaunchCatalog.LaunchParameter"/> or
    /// <see cref="MissionControl.MissionParameter"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
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
        public override Constraint DeepClone()
        {
            ListConstraint ret = new();
            ret.Choices = Choices.ToList();
            return ret;
        }

        public bool Equals(ListConstraint other)
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
