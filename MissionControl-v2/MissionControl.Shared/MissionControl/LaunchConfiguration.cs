using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Launch configuration of a MissionControl's mission.
    /// </summary>
    /// <remarks>Parameters and Devices property is still to be implemented</remarks>
    public class LaunchConfiguration: ObservableObject, IEquatable<LaunchConfiguration>
    {
        /// <summary>
        /// Identifier of the <see cref="Asset"/> used by this mission.
        /// </summary>
        public Guid AssetId { get; set; }

        /// <summary>
        /// Assets <see cref="LaunchCatalog.LaunchableBase.GlobalParameters"/>s value.
        /// </summary>
        public IEnumerable<LaunchParameterValue> Parameters { get; set; } =
            Enumerable.Empty<LaunchParameterValue>();

        /// <summary>
        /// Configuration of every <see cref="LaunchComplex"/> participating to the mission.
        /// </summary>
        public IEnumerable<LaunchComplexConfiguration> LaunchComplexes { get; set; } =
            Enumerable.Empty<LaunchComplexConfiguration>();

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public LaunchConfiguration DeepClone()
        {
            LaunchConfiguration ret = new();
            ret.DeepCopyFrom(this);
            return ret;
        }

        /// <summary>
        /// Copy from the given object to create an independent copy (no data is be shared between the original and the
        /// clone).
        /// </summary>
        /// <param name="from">To copy from.</param>
        public void DeepCopyFrom(LaunchConfiguration from)
        {
            AssetId = from.AssetId;
            Parameters = from.Parameters.Select(p => p.DeepClone()).ToList();
            LaunchComplexes = from.LaunchComplexes.Select(lp => lp.DeepClone()).ToList();
        }

        public bool Equals(LaunchConfiguration? other)
        {
            return other != null &&
                AssetId == other.AssetId &&
                Parameters.SequenceEqual(other.Parameters) &&
                LaunchComplexes.SequenceEqual(other.LaunchComplexes);
        }
    }
}
