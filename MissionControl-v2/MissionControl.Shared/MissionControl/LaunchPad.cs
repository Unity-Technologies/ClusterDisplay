using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a LaunchPad.
    /// </summary>
    public class LaunchPad: IEquatable<LaunchPad>
    {
        /// <summary>
        /// Unique identifier of the LaunchPad.
        /// </summary>
        /// <remarks>Shall be the same as LaunchPad's configuration's identifier of the LaunchPad at the endpoint
        /// property.</remarks>
        public Guid Identifier { get; set; }

        /// <summary>
        /// User displayed name identifying this LaunchPad.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Http endpoint of the LaunchPad.
        /// </summary>
        public Uri Endpoint {
            get => m_Endpoint;
            set => m_Endpoint = new Uri(value.ToString());
        }
        Uri m_Endpoint = new Uri("http://0.0.0.0/");

        /// <summary>
        /// Types of Launchables that this LaunchPad can deal with.
        /// </summary>
        public IEnumerable<string> SuitableFor { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public LaunchPad DeepClone()
        {
            LaunchPad ret = new();
            ret.Identifier = Identifier;
            ret.Name = Name;
            ret.Endpoint = Endpoint;
            ret.SuitableFor = SuitableFor.ToList();
            return ret;
        }

        public bool Equals(LaunchPad? other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                Name == other.Name &&
                Endpoint == other.Endpoint &&
                SuitableFor.SequenceEqual(other.SuitableFor);
        }
    }
}
