using System.Text.RegularExpressions;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Give the regular expression to validate a string <see cref="LaunchParameter"/>.
    /// </summary>
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
        public string RegularExpression { get; set; } = ".*";

        /// <inheritdoc/>
        public override bool Validate(object value)
        {
            var valueAsString = value.ToString();
            if (valueAsString == null)
            {
                return false;
            }
            var regexMatch = Regex.Match(valueAsString, RegularExpression);
            return regexMatch.Success && regexMatch.Length == valueAsString.Length;
        }

        /// <inheritdoc/>
        public override Constraint DeepClone()
        {
            RegularExpressionConstraint ret = new();
            ret.RegularExpression = RegularExpression;
            return ret;
        }

        public bool Equals(RegularExpressionConstraint? other)
        {
            return other != null && other.GetType() == typeof(RegularExpressionConstraint) &&
                RegularExpression == other.RegularExpression;
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((RegularExpressionConstraint)other);
        }
    }
}
