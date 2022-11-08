using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Value of a <see cref="LaunchCatalog.LaunchParameter"/>.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class LaunchParameterValue: IEquatable<LaunchParameterValue>
    {
        /// <summary>
        /// Identifier of the launch parameter (matches <see cref="LaunchCatalog.LaunchParameter.Id"/>).
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// The actual value of a launch parameter.
        /// </summary>
        /// <remarks>Should ideally be a <see cref="bool"/>, <see cref="int"/>, <see cref="float"/> or
        /// <see cref="string"/>.  <see cref="long"/> or <see cref="double"/> are also accepted to simplify Json.net
        /// deserialization (but they will be converted to <see cref="int"/> or <see cref="float"/>).  Cannot be null
        /// (if value for a parameter is to be omitted / default value, just do not specify it).
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

        public bool Equals(LaunchParameterValue other)
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
