using System;
using Newtonsoft.Json;

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
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    [JsonConverter(typeof(ConstraintJsonConverter))]
    public abstract class Constraint: IEquatable<Constraint>
    {
        /// <summary>
        /// Type of constraint.
        /// </summary>
        // ReSharper disable once MemberCanBeProtected.Global -> Need to be public for simple json serialization
        public ConstraintType Type { get; protected set; }

        /// <summary>
        /// Compares with a <see cref="Constraint"/> of the same type (already validated by the caller of the method).
        /// </summary>
        /// <param name="other">The other <see cref="Constraint"/> of the same type.</param>
        protected abstract bool EqualsOfSameType(Constraint other);

        public bool Equals(Constraint other)
        {
            return other != null &&
                other.Type == Type &&
                other.EqualsOfSameType(this);
        }
    }
}
