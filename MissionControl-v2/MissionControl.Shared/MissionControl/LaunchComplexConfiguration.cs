using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Configuration of a <see cref="LaunchComplex"/> for a mission.
    /// </summary>
    /// <remarks>Parameters property is still to be implemented</remarks>
    public class LaunchComplexConfiguration: IEquatable<LaunchComplexConfiguration>
    {
        /// <summary>
        /// <see cref="LaunchComplex"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Configuration of every <see cref="LaunchPad"/> of this <see cref="LaunchComplex"/> participating to the
        /// mission.
        /// </summary>
        public IEnumerable<LaunchPadConfiguration> LaunchPads { get; set; } =
            Enumerable.Empty<LaunchPadConfiguration>();

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        /// <returns></returns>
        public LaunchComplexConfiguration DeepClone()
        {
            LaunchComplexConfiguration ret = new();
            ret.Identifier = Identifier;
            ret.LaunchPads = LaunchPads.Select(lp => lp.DeepClone()).ToList();
            return ret;
        }

        public void DeepCopy(LaunchComplexConfiguration from)
        {
            Identifier = from.Identifier;
            LaunchPads = from.LaunchPads.Select(lp => lp.DeepClone()).ToList();
        }

        public bool Equals(LaunchComplexConfiguration? other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                LaunchPads.SequenceEqual(other.LaunchPads);
        }
    }
}
