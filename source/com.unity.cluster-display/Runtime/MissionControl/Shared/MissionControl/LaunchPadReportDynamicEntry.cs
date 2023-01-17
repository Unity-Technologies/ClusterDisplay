using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// An entry in the status or health of a <see cref="LaunchPad"/> that depends on what it runs (dynamicEntries).
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
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
                else if (value is long)
                {
                    m_Value = Convert.ToInt32(value);
                }
                else if (value is double)
                {
                    m_Value = Convert.ToSingle(value);
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

        // ReSharper disable once MemberCanBePrivate.Global
        public void DeepCopyFrom(LaunchPadReportDynamicEntry from)
        {
            Name = from.Name;
            Value = from.Value;
        }

        public bool Equals(LaunchPadReportDynamicEntry other)
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
