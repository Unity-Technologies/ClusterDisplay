using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Give the regular expression to validate a string <see cref="LaunchParameter"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class RegularExpressionConstraint: Constraint, IEquatable<RegularExpressionConstraint>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RegularExpressionConstraint()
        {
            Type = ConstraintType.RegularExpression;
        }

        /// <summary>
        /// The Regular Expression.
        /// </summary>
        public String RegularExpression { get; set; } = ".*";

        /// <summary>
        /// Error message to display if regular expression is not valid.
        /// </summary>
        public string ErrorMessage { get; set; } = "Value not valid";

        public bool Equals(RegularExpressionConstraint other)
        {
            return other != null && other.GetType() == typeof(RegularExpressionConstraint) &&
                RegularExpression == other.RegularExpression &&
                ErrorMessage == other.ErrorMessage;
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((RegularExpressionConstraint)other);
        }
    }
}
