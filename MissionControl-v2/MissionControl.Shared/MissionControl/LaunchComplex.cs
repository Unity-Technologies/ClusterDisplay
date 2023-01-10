using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a LaunchComplex (generally a computer).
    /// </summary>
    /// <remarks><see cref="IIncrementalCollectionObject.Id"/> shall match <see cref="HangarBay"/>'s Identifier
    /// property.</remarks>
    public class LaunchComplex: IIncrementalCollectionObject, IEquatable<LaunchComplex>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchComplex(Guid id)
        {
            Id = id;
            HangarBay.Identifier = id;
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <summary>
        /// User displayed name identifying this LaunchComplex.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// LaunchPads forming this LaunchComplex.
        /// </summary>
        public IEnumerable<LaunchPad> LaunchPads { get; set; } = Enumerable.Empty<LaunchPad>();

        /// <summary>
        /// HangarBay of the LaunchComplex.
        /// </summary>
        public HangarBay HangarBay { get; set; } = new();

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (LaunchComplex)fromObject;
            Name = from.Name;
            LaunchPads = from.LaunchPads.Select(lp => lp.DeepClone()).ToList();
            HangarBay = from.HangarBay.DeepClone();
        }

        public bool Equals(LaunchComplex? other)
        {
            return other != null &&
                Id == other.Id &&
                Name == other.Name &&
                LaunchPads.SequenceEqual(other.LaunchPads) &&
                HangarBay.Equals(other.HangarBay);
        }
    }
}
