using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about an HangarBay.
    /// </summary>
    public class HangarBay: IEquatable<HangarBay>
    {
        /// <summary>
        /// Unique identifier of the HangarBay.
        /// </summary>
        /// <remarks>Shall be the same as HangarBay's configuration's identifier of the HangarBay at the endpoint
        /// property.</remarks>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Http endpoint of the HangarBay.
        /// </summary>
        /// <remarks>Cannot use auto property because we want to store the normalized uri so that this is the one that
        /// gets serialized to json.</remarks>
        public Uri Endpoint {
            get => m_Endpoint;
            set => m_Endpoint = new Uri(value.ToString());
        }
        Uri m_Endpoint = new Uri("http://0.0.0.0/");

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public HangarBay DeepClone()
        {
            HangarBay ret = new();
            ret.Identifier = Identifier;
            ret.Endpoint = Endpoint;
            return ret;
        }

        public bool Equals(HangarBay? other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                Endpoint == other.Endpoint;
        }
    }
}
