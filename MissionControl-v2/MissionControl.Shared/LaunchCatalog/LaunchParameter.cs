using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Type of a <see cref="LaunchParameter"/>.
    /// </summary>
    public enum LaunchParameterType
    {
        Integer,
        Float,
        String
    }

    /// <summary>
    /// Parameter to customize launching behavior of a <see cref="LaunchableBase"/>.
    /// </summary>
    public class LaunchParameter: IEquatable<LaunchParameter>
    {
        /// <summary>
        /// Name of the parameter as displayed to the user.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Name of the group (when displaying the parameter to allow to see or set its value) this parameter is a part
        /// of.  Nested groups can be expressed by separating them with a slash (/).
        /// </summary>
        public string Group { get; set; } = "";

        /// <summary>
        /// Identifier of the parameter that will be used to produce the information passed through the LAUNCH_DATA
        /// environment variable (must be unique among all <see cref="LaunchParameter"/>s of a
        /// <see cref="LaunchableBase"/>).
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Detailed description of the parameter (that could for example be displayed in a tooltip).
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Type of the value of the <see cref="LaunchParameter"/>.
        /// </summary>
        /// <exception cref="FormatException"><see cref="DefaultValue"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><see cref="DefaultValue"/> Cannot be converted to
        /// the new <see cref="LaunchParameter"/> value.</exception>
        /// <exception cref="OverflowException"><see cref="DefaultValue"/> represents a value that is too large (or
        /// small) to fit in the new <see cref="LaunchParameterType"/> value.</exception>
        public LaunchParameterType? Type
        {
            get => m_Type;
            set
            {
                // Remark: We try to set m_DefaultValue with the new type before updating m_Type so that this object
                // stay valid if the new combination is not.
                m_DefaultValue = ConvertValueToType(value, DefaultValue);
                m_Type = value;
            }
        }

        /// <summary>
        /// Constraint for this parameter.
        /// </summary>
        public Constraint? Constraint { get; set; }

        /// <summary>
        /// Default value of the property (type of this property must match <see cref="Type"/>).
        /// </summary>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format
        /// (<see cref="Type"/>).</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to
        /// <see cref="Type"/>.</exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="Type"/>.</exception>
        public object? DefaultValue
        {
            get => m_DefaultValue;
            set => m_DefaultValue = ConvertValueToType(Type, value);
        }

        public bool Equals(LaunchParameter? other)
        {
            if (other == null || (Constraint == null) != (other.Constraint == null))
            {
                return false;
            }

            return other.GetType() == typeof(LaunchParameter) &&
                other.Name == Name &&
                other.Group == Group &&
                other.Id == Id &&
                other.Description == Description &&
                Object.Equals(other.m_Type, m_Type) &&
                (other.Constraint == null || other.Constraint.Equals(Constraint)) &&
                Object.Equals(other.m_DefaultValue, m_DefaultValue);
        }

        /// <summary>
        /// Convert a value to <see cref="LaunchParameterType"/>.
        /// </summary>
        /// <param name="type"><see cref="LaunchParameterType"/>.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to
        /// <see cref="LaunchParameterType"/>.</exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="LaunchParameterType"/>.</exception>
        object? ConvertValueToType(LaunchParameterType? type, object? value)
        {
            // Quick check, anything is acceptable if one of them is unknown.
            if (!type.HasValue || value == null)
            {
                return value;
            }

            switch (type.Value)
            {
                case LaunchParameterType.Integer:
                    return ConvertToInt32(value);
                case LaunchParameterType.Float:
                    return ConvertToSingle(value);
                case LaunchParameterType.String:
                    return ConvertToString(value);
                default:
                    throw new ArgumentException("Unsupported enum constant", nameof(type));
            }
        }

        /// <summary>
        /// Convert the provided value to a <see cref="int"/>.
        /// </summary>
        /// <param name="value">The value</param>
        /// <remarks>We are not using Convert.ToInt32 for <see cref="float"/> or <see cref="double"/> as it would result
        /// in decimal portion of the value to be rounded off without any kind of warning.  We want to avoid loosing
        /// part of the information.</remarks>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to <see cref="int"/>.
        /// </exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="int"/>.</exception>
        static int ConvertToInt32(object value)
        {
            if (value is float floatValue)
            {
                int asAInt32 = (int)floatValue;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (asAInt32 == floatValue)
                {
                    return asAInt32;
                }
                else
                {
                    throw new FormatException($"Converting {floatValue} to {typeof(int)} would result in lost information.");
                }
            }
            if (value is double doubleValue)
            {
                int asAInt32 = (int)doubleValue;
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (asAInt32 == doubleValue)
                {
                    return asAInt32;
                }
                else
                {
                    throw new FormatException($"Converting {doubleValue} to {typeof(int)} would result in lost information.");
                }
            }
            if (value is JsonElement jsonValue)
            {
                if (jsonValue.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                throw new InvalidCastException($"Cannot convert {jsonValue.ToString()} to {typeof(int)}.");
            }
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Convert the provided value to a <see cref="float"/>.
        /// </summary>
        /// <param name="value">The value</param>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to <see cref="float"/>.
        /// </exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="float"/>.</exception>
        static float ConvertToSingle(object value)
        {
            if (value is JsonElement jsonValue)
            {
                if (jsonValue.TryGetSingle(out float floatValue))
                {
                    return floatValue;
                }
                throw new InvalidCastException($"Cannot convert {jsonValue.ToString()} to {typeof(float)}.");
            }
            float ret = Convert.ToSingle(value);
            if (float.IsInfinity(ret))
            {
                throw new OverflowException($"Converting to {value} to {typeof(float)} leads to infinity and beyond...");
            }
            if (!float.IsNormal(ret))
            {
                throw new InvalidCastException($"Cannot convert {value} to {typeof(float)}.");
            }
            return ret;
        }

        /// <summary>
        /// Convert the provided value to a <see cref="string"/>.
        /// </summary>
        /// <param name="value">The value</param>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to <see cref="float"/>.
        /// </exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="float"/>.</exception>
        static string ConvertToString(object value)
        {
            if (value is JsonElement jsonValue)
            {
                if (jsonValue.ValueKind == JsonValueKind.String)
                {
                    return jsonValue.GetString()!;
                }
                throw new InvalidCastException($"Cannot convert {jsonValue.ToString()} to {typeof(string)}.");
            }
            var convertedString = Convert.ToString(value);
            if (convertedString == null)
            {
                throw new NullReferenceException("Converting to string resulted in a null value.");
            }
            return convertedString;
        }

        /// <summary>
        /// Type of the value of the <see cref="LaunchParameter"/>.
        /// </summary>
        LaunchParameterType? m_Type;

        /// <summary>
        /// Default value of the property (type of this property must match <see cref="Type"/>).
        /// </summary>
        object? m_DefaultValue;
    }
}
