using System.Text.Json;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Value of a <see cref="LaunchParameter"/>.
    /// </summary>
    public class LaunchParameterValue: IEquatable<LaunchParameterValue>
    {
        /// <summary>
        /// Identifier of the launch parameter (matches <see cref="LaunchParameter.Id"/>).
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// The actual value of a launch parameter.
        /// </summary>
        /// <remarks>Has to be a <see cref="bool"/>, <see cref="int"/>, <see cref="float"/> or <see cref="string"/>.
        /// Cannot be null (if value for a parameter is to be omitted / default value, just do not specify it).
        /// </remarks>
        public object Value
        {
            get => m_Value;
            set
            {
                if (value is bool or int or float or string)
                {
                    m_Value = value;
                }
                else if (value is JsonElement jsonElement)
                {
                    switch (jsonElement.ValueKind)
                    {
                        case JsonValueKind.True:
                            m_Value = true;
                            break;
                        case JsonValueKind.False:
                            m_Value = false;
                            break;
                        case JsonValueKind.Number:
                            if (jsonElement.TryGetInt64(out var int64))
                            {
                                m_Value = Convert.ToInt32(int64);
                            }
                            else
                            {
                                m_Value = Convert.ToSingle(jsonElement.GetDecimal());
                            }
                            break;
                        case JsonValueKind.String:
                            m_Value = jsonElement.GetString()!;
                            break;
                        default:
                            throw new ArgumentException("Unsupported json value type, has to be a boolean, number or string.", nameof(value));
                    }
                }
                else
                {
                    throw new ArgumentException("Value has to be a bool, int, float or string.", nameof(value));
                }
            }
        }

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public LaunchParameterValue DeepClone()
        {
            LaunchParameterValue ret = new();
            ret.Id = Id;
            ret.Value = Value; // I agree it does not look like a deep clone, but remember, it is a int, float or string.
            return ret;
        }

        public bool Equals(LaunchParameterValue? other)
        {
            return other != null &&
                Id == other.Id &&
                Value.Equals(other.Value);
        }

        /// <summary>
        /// Storage for the actual value of a launch parameter.
        /// </summary>
        /// <remarks>Will be of type <see cref="bool"/>, <see cref="int"/>, <see cref="float"/> or <see cref="string"/>.
        /// </remarks>
        object m_Value = 0;
    }
}
