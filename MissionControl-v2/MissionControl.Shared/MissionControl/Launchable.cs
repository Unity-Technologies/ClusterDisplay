namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Represent something that can be launched on a launchpad.
    /// </summary>
    /// <remarks>Splitting of this class in <see cref="Launchable"/> and <see cref="LaunchCatalog.LaunchableBase"/>
    /// is more about avoiding copy / paste and ensuring everything that should be the same is the same than true
    /// polymorphism: code should normally deal with <see cref="Launchable"/> or <see cref="LaunchCatalog.Launchable"/>,
    /// not <see cref="LaunchCatalog.LaunchableBase"/>.</remarks>
    public class Launchable: LaunchCatalog.LaunchableBase, IEquatable<Launchable>
    {
        /// <summary>
        /// Name of Payloads forming the list of all the files needed by this <see cref="LaunchCatalog.LaunchableBase"/>.
        /// </summary>
        public IEnumerable<Guid> Payloads { get; set; } = Enumerable.Empty<Guid>();

        /// <summary>
        /// Create a shallow copy of from.
        /// </summary>
        /// <param name="from">To copy from.</param>
        public void ShallowCopy(Launchable from)
        {
            base.ShallowCopy(from);
            Payloads = from.Payloads;
        }

        public bool Equals(Launchable? other)
        {
            if (other == null || other.GetType() != typeof(Launchable))
            {
                return false;
            }

            return ArePropertiesEqual(other) &&
                Payloads.SequenceEqual(other.Payloads);
        }
    }
}
