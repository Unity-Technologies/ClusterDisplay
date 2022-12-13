using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Configuration of a <see cref="LaunchPad"/> for a mission.
    /// </summary>
    public class LaunchPadConfiguration: IEquatable<LaunchPadConfiguration>
    {
        /// <summary>
        /// <see cref="LaunchPad"/>'s identifier.
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Assets <see cref="LaunchCatalog.LaunchableBase.LaunchPadParameters"/>s value.
        /// </summary>
        public IEnumerable<LaunchParameterValue> Parameters { get; set; } =
            Enumerable.Empty<LaunchParameterValue>();

        /// <summary>
        /// Name of the <see cref="Launchable"/> to launch on this launchpad.
        /// </summary>
        public string LaunchableName { get; set; } = "";

        /// <summary>
        /// Returns the <see cref="Launchable"/> that will be used for this <see cref="LaunchPadConfiguration"/>.
        /// </summary>
        /// <param name="asset">The asset to search in for <see cref="Launchable"/>s.</param>
        /// <param name="launchPad">The <see cref="LaunchPad"/> this <see cref="LaunchPadConfiguration"/> is
        /// configuring.</param>
        /// <remarks>Returns the one with a matching name or the first one in alphabetical order is not found and
        /// <see cref="LaunchableName"/> is not empty.</remarks>
        public Launchable? GetEffectiveLaunchable(Asset asset, LaunchPad launchPad)
        {
            if (LaunchableName == "")
            {
                return null;
            }

            var compatible = launchPad.GetCompatibleLaunchables(asset).ToList();
            var withTheRightName = compatible.FirstOrDefault(l => l.Name == LaunchableName);
            if (withTheRightName != null)
            {
                return withTheRightName;
            }
            else
            {
                return compatible.MinBy(l => l.Name);
            }
        }

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        public LaunchPadConfiguration DeepClone()
        {
            LaunchPadConfiguration ret = new();
            ret.DeepCopyFrom(this);
            return ret;
        }

        public void DeepCopyFrom(LaunchPadConfiguration from)
        {
            Identifier = from.Identifier;
            Parameters = from.Parameters.Select(p => p.DeepClone()).ToList();
            LaunchableName = from.LaunchableName;
        }

        public bool Equals(LaunchPadConfiguration? other)
        {
            return other != null &&
                Identifier == other.Identifier &&
                Parameters.SequenceEqual(other.Parameters) &&
                LaunchableName == other.LaunchableName;
        }
    }
}
