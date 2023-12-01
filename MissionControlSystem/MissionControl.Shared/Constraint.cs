using System.Text.Json.Serialization;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Type of <see cref="Constraint"/>.
    /// </summary>
    public enum ConstraintType
    {
        /// <summary>
        /// <see cref="ConfirmationConstraint"/>
        /// </summary>
        Confirmation,
        /// <summary>
        /// <see cref="ListConstraint"/>
        /// </summary>
        List,
        /// <summary>
        /// <see cref="RangeConstraint"/>
        /// </summary>
        Range,
        /// <summary>
        /// <see cref="RegularExpressionConstraint"/>
        /// </summary>
        RegularExpression
    }

    /// <summary>
    /// Base class for constraints of <see cref="LaunchCatalog.LaunchParameter"/> or
    /// <see cref="MissionControl.MissionParameter"/>.
    /// </summary>
    [JsonConverter(typeof(ConstraintJsonConverter))]
    public abstract class Constraint : IEquatable<Constraint>
    {
        /// <summary>
        /// Type of constraint.
        /// </summary>
        public ConstraintType Type { get; protected set; }

        /// <summary>
        /// Validates that the given value respect the constraints.
        /// </summary>
        /// <param name="value">The value to test.</param>
        public abstract bool Validate(object value);

        /// <summary>
        /// Compares with a <see cref="Constraint"/> of the same type (already validated by the caller of the method).
        /// </summary>
        /// <param name="other">The other <see cref="Constraint"/> of the same type.</param>
        protected abstract bool EqualsOfSameType(Constraint other);

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public abstract Constraint DeepClone();

        public bool Equals(Constraint? other)
        {
            return other != null &&
                other.Type == Type &&
                other.EqualsOfSameType(this);
        }
    }
}
