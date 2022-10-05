using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Give the range value for integer or float <see cref="LaunchParameter"/>.
    /// </summary>
    public class RangeConstraint: Constraint, IEquatable<RangeConstraint>
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
        public object? Min
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool MinExclusive{ get; set; }

        /// <summary>
        /// Maximum value allowed (<c>null</c> for no maximum value).
        /// </summary>
        /// <remarks>Value is either <see cref="int"/> or <see cref="float"/>.</remarks>
        public object? Max
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool MaxExclusive { get; set; }

        public bool Equals(RangeConstraint? other)
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
        static object? ConvertObjectToIntOrFloat(object? value)
        {
            if (value is int or float or null)
            {
                return value;
            }
            if (value is double doubleValue)
            {
                return (float)doubleValue;
            }
            else if (value is JsonElement jsonValue)
            {
                return ConvertJsonElementToValueObject(jsonValue);
            }
            else
            {
                return Convert.ToInt32(value);
            }
        }

        /// <summary>
        /// Convert a <see cref="JsonElement"/> to an <see cref="int"/> or <see cref="float"/>.
        /// </summary>
        /// <param name="jsonElement">Containing the json to convert to a number.</param>
        /// <returns></returns>
        static object ConvertJsonElementToValueObject(JsonElement jsonElement)
        {
            if (jsonElement.TryGetInt32(out int intValue))
            {
                return intValue;
            }
            if (jsonElement.TryGetSingle(out float floatValue))
            {
                return floatValue;
            }
            throw new InvalidCastException($"Cannot convert {jsonElement.ToString()} to {typeof(int)} or {typeof(float)}");
        }

        /// <summary>
        /// Minimum value
        /// </summary>
        object? m_Min;

        /// <summary>
        /// Maximum value
        /// </summary>
        object? m_Max;
    }
}
