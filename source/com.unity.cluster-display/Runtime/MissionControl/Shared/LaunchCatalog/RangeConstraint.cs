using System;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Give the range value for integer or float <see cref="LaunchParameter"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class RangeConstraint : Constraint, IEquatable<RangeConstraint>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RangeConstraint()
        {
            Type = ConstraintType.Range;
        }

        /// <summary>
        /// Minimum value allowed (<c>null</c> for no minimum value).
        /// </summary>
        /// <remarks>Value is either <see cref="int"/> or <see cref="float"/>.</remarks>
        [CanBeNull]
        public object Min
        {
            get => m_Min;
            set => m_Min = ConvertObjectToIntOrFloat(value);
        }

        /// <summary>
        /// <see cref="Min"/> as an <see cref="int"/>.
        /// </summary>
        [JsonIgnore]
        public int MinInt32 => Convert.ToInt32(Min);

        /// <summary>
        /// <see cref="Min"/> as an <see cref="float"/>.
        /// </summary>
        [JsonIgnore]
        public float MinSingle => Convert.ToSingle(Min);

        /// <summary>
        /// Is the allowed range defined with &gt;= (true) or &gt; (false).
        /// </summary>
        public bool MinExclusive { get; set; }

        /// <summary>
        /// Method to instruct Json.Net to only serialize <see cref="MinExclusive"/> if is <c>true</c>.
        /// </summary>
        public bool ShouldSerializeMinExclusive() => MinExclusive;

        /// <summary>
        /// Maximum value allowed (<c>null</c> for no maximum value).
        /// </summary>
        /// <remarks>Value is either <see cref="int"/> or <see cref="float"/>.</remarks>
        [CanBeNull]
        public object Max
        {
            get => m_Max;
            set => m_Max = ConvertObjectToIntOrFloat(value);
        }

        /// <summary>
        /// <see cref="Max"/> as an <see cref="int"/>.
        /// </summary>
        [JsonIgnore]
        public int MaxInt32 => Convert.ToInt32(Max);

        /// <summary>
        /// <see cref="Max"/> as an <see cref="float"/>.
        /// </summary>
        [JsonIgnore]
        public float MaxSingle => Convert.ToSingle(Max);

        /// <summary>
        /// Is the allowed range defined with &lt; (true) or &lt;= (false).
        /// </summary>
        public bool MaxExclusive { get; set; }

        /// <summary>
        /// Method to instruct Json.Net to only serialize <see cref="MaxExclusive"/> if is <c>true</c>.
        /// </summary>
        public bool ShouldSerializeMaxExclusive() => MaxExclusive;

        public bool Equals(RangeConstraint other)
        {
            return other != null && other.GetType() == typeof(RangeConstraint) &&
                Object.Equals(m_Min, other.m_Min) && MinExclusive == other.MinExclusive &&
                Object.Equals(m_Max, other.m_Max) && MaxExclusive == other.MaxExclusive;
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((RangeConstraint)other);
        }

        /// <summary>
        /// Convert an arbitrary object
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [CanBeNull]
        static object ConvertObjectToIntOrFloat([CanBeNull] object value)
        {
            return value switch
            {
                int or float or null => value,
                double doubleValue => (float)doubleValue,
                _ => Convert.ToInt32(value)
            };
        }

        /// <summary>
        /// Minimum value
        /// </summary>
        [CanBeNull]
        object m_Min;

        /// <summary>
        /// Maximum value
        /// </summary>
        [CanBeNull]
        object m_Max;
    }
}
