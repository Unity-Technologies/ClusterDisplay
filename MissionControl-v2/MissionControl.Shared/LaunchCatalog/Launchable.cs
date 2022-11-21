using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchCatalog
{
    /// <summary>
    /// An entry of the catalog describing something that can be launched.
    /// </summary>
    /// <remarks>Splitting of this class in <see cref="Launchable"/> and <see cref="LaunchableBase"/> is more about
    /// avoiding copy / paste and ensuring everything that should be the same is the same than true polymorphism: code
    /// should normally deal with <see cref="Launchable"/> or <see cref="MissionControl.Launchable"/>, not
    /// <see cref="LaunchableBase"/>.</remarks>
    public class Launchable: LaunchableBase, IEquatable<Launchable>
    {
        /// <summary>
        /// Name of Payloads forming the list of all the files needed by this <see cref="LaunchableBase"/>.
        /// </summary>
        public IEnumerable<string> Payloads { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Create a shallow copy of from.
        /// </summary>
        /// <param name="from">To copy from.</param>
        public void ShallowCopyFrom(Launchable from)
        {
            base.ShallowCopyFrom(from);
            Payloads = from.Payloads;
        }

        public bool Equals(Launchable? other)
        {
            return other != null &&
                ArePropertiesEqual(other) &&
                Payloads.SequenceEqual(other.Payloads);
        }
    }
}
