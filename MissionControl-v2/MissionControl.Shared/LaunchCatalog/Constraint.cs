using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Type of <see cref="Constraint"/>.
    /// </summary>
    public enum ConstraintType
    {
        /// <summary>
        /// <see cref="RangeConstraint"/>
        /// </summary>
        Range,
        /// <summary>
        /// <see cref="RegularExpressionConstraint"/>
        /// </summary>
        RegularExpression,
        /// <summary>
        /// <see cref="ListConstraint"/>
        /// </summary>
        List
    }

    /// <summary>
    /// Base class for constraints of <see cref="LaunchParameter"/>.
    /// </summary>
    [JsonConverter(typeof(ConstraintJsonConverter))]
    public abstract class Constraint: IEquatable<Constraint>
    {
        /// <summary>
        /// Type of constraint.
        /// </summary>
        public ConstraintType Type { get; protected set; }

        /// <summary>
        /// Method to be implemented by specializing classes to compare with a <see cref="Constraint"/> of the same
        /// type (already validated by the caller of the method).
        /// </summary>
        /// <param name="other">The other <see cref="Constraint"/> of the same type.</param>
        protected abstract bool EqualsOfSameType(Constraint other);

        public bool Equals(Constraint? other)
        {
            if (other == null || other.GetType() != GetType() || other.Type != Type)
            {
                return false;
            }

            return other.EqualsOfSameType(this);
        }
    }
}
