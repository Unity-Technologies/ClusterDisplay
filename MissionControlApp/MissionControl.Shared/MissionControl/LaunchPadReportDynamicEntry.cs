using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// An entry in the status or health of a <see cref="LaunchPad"/> that depends on what it runs (dynamicEntries).
    /// </summary>
    public class LaunchPadReportDynamicEntry: IEquatable<LaunchPadReportDynamicEntry>
    {
        /// <summary>
        /// Name (to be displayed to the user) of the status or health value.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Value of the dynamic status or health entry.
        /// </summary>
        /// <remarks>Supported types: <see cref="bool"/>, <see cref="int"/>, <see cref="float"/> and
        /// <see cref="string"/>.</remarks>
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
                    case JsonValueKind.String:
                        m_Value = jsonElement.GetString()!;
                        break;
                    case JsonValueKind.Number:
                        if (jsonElement.TryGetInt32(out var intValue))
                        {
                            m_Value = intValue;
                        }
                        else if (jsonElement.TryGetSingle(out var floatValue))
                        {
                            m_Value = floatValue;
                        }
                        else
                        {
                            throw new ArgumentException($"Cannot convert json numerical value to int or float: " +
                                $"{jsonElement}");
                        }
                        break;
                    case JsonValueKind.True:
                        m_Value = true;
                        break;
                    case JsonValueKind.False:
                        m_Value = false;
                        break;
                    default:
                        throw new ArgumentException($"JsonElement cannot be converted to bool, int, float or string: " +
                            $"{jsonElement}.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Value must be a bool, int, float or string, was passed a " +
                        $"{value.GetType()}.");
                }
            }
        }

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public LaunchPadReportDynamicEntry DeepClone()
        {
            LaunchPadReportDynamicEntry ret = new();
            ret.DeepCopyFrom(this);
            return ret;
        }

        public void DeepCopyFrom(LaunchPadReportDynamicEntry from)
        {
            Name = from.Name;
            Value = from.Value;
        }

        public bool Equals(LaunchPadReportDynamicEntry? other)
        {
            return other != null &&
                Name == other.Name &&
                Value.Equals(other.Value);
        }

        /// <summary>
        /// Storage for <see cref="Value"/>.
        /// </summary>
        object m_Value = 0;
    }
}
