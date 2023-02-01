using System;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// Type of a <see cref="LaunchParameter"/>.
    /// </summary>
    public enum LaunchParameterType
    {
        Boolean,
        Integer,
        Float,
        String
    }

    /// <summary>
    /// Parameter to customize launching behavior of a <see cref="Launchable"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchParameter : IEquatable<LaunchParameter>
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
        /// <see cref="Launchable"/>).
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
        [CanBeNull]
        public Constraint Constraint { get; set; }

        /// <summary>
        /// Default value of the property (type of this property must match <see cref="Type"/>).
        /// </summary>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format
        /// (<see cref="Type"/>).</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to
        /// <see cref="Type"/>.</exception>
        /// <exception cref="OverflowException"><paramref name="value"/> represent a value that is too large (or small)
        /// to fit in a <see cref="Type"/>.</exception>
        [CanBeNull]
        public object DefaultValue
        {
            get => m_DefaultValue;
            set => m_DefaultValue = ConvertValueToType(Type, value);
        }

        /// <summary>
        /// Does the value of this parameter need to be revised by capcom before proceeding to launch?
        /// </summary>
        public bool ToBeRevisedByCapcom { get; set; }

        /// <summary>
        /// Indicate that the parameter is not to be displayed to the user.
        /// </summary>
        /// <remarks>Especially useful when combined with <see cref="ToBeRevisedByCapcom"/> to have some parameters
        /// computed automatically by capcom (as a consequence of many other parameters in different launchpads for
        /// example).</remarks>
        public bool Hidden { get; set; }

        public bool Equals(LaunchParameter other)
        {
            if (other == null || (Constraint == null) != (other.Constraint == null))
            {
                return false;
            }

            return other.Name == Name &&
                other.Group == Group &&
                other.Id == Id &&
                other.Description == Description &&
                Equals(other.m_Type, m_Type) &&
                (other.Constraint == null || other.Constraint.Equals(Constraint)) &&
                Equals(other.m_DefaultValue, m_DefaultValue) &&
                other.ToBeRevisedByCapcom == ToBeRevisedByCapcom &&
                other.Hidden == Hidden;
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
        [CanBeNull]
        static object ConvertValueToType(LaunchParameterType? type, [CanBeNull] object value)
        {
            // Quick check, anything is acceptable if one of them is unknown.
            if (!type.HasValue || value == null)
            {
                return value;
            }

            switch (type.Value)
            {
                case LaunchParameterType.Boolean:
                    return ConvertToBoolean(value);
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
        /// Convert the provided value to a <see cref="bool"/>.
        /// </summary>
        /// <param name="value">The value</param>
        /// <remarks>All numbers are converted to true, except 0.  True or False strings are also accepted (case
        /// insensitive).</remarks>
        /// <exception cref="FormatException"><paramref name="value"/> is not in an appropriate format.</exception>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to <see cref="bool"/>.
        /// </exception>
        static bool ConvertToBoolean(object value)
        {
            return Convert.ToBoolean(value);
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
                int roundedValue = (int)Math.Round(floatValue);
                float distanceFromInt = floatValue - roundedValue;

                // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types
                // say single have ~6-9 digits of precision, so we use 1.0E-6f as the threshold.
                if (distanceFromInt <= 1.0E-6f)
                {
                    return roundedValue;
                }
                else
                {
                    throw new FormatException($"Converting {floatValue} to {typeof(int)} would result in lost information.");
                }
            }

            if (value is double doubleValue)
            {
                int roundedValue = (int)Math.Round(doubleValue);
                double distanceFromInt = doubleValue - roundedValue;

                // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/floating-point-numeric-types
                // say single have ~15-17 digits of precision, so we use 1.0E-15f as the threshold.
                if (distanceFromInt <= 1.0E-15f)
                {
                    return roundedValue;
                }
                else
                {
                    throw new FormatException($"Converting {doubleValue} to {typeof(int)} would result in lost information.");
                }
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
            float ret = Convert.ToSingle(value);
            if (!float.IsFinite(ret))
            {
                throw new InvalidCastException($"Cannot convert {value} to {typeof(float)}.");
            }

            return ret;
        }

        /// <summary>
        /// Convert the provided value to a <see cref="string"/>.
        /// </summary>
        /// <param name="value">The value</param>
        /// <exception cref="InvalidCastException"><paramref name="value"/> Cannot be converted to <see cref="string"/>.
        /// </exception>
        /// <exception cref="NullReferenceException">There was an error converting <paramref name="value"/> to a string.
        /// </exception>
        static string ConvertToString(object value)
        {
            if (value.GetType() == typeof(JArray))
            {
                throw new InvalidCastException($"Cannot convert {value} to {typeof(string)}.");
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
        [CanBeNull]
        object m_DefaultValue;
    }
}
