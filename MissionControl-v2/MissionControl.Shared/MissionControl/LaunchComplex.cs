using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Information about a LaunchComplex (generally a computer).
    /// </summary>
    /// <remarks><see cref="IncrementalCollectionObject.Id"/> shall match <see cref="HangarBay"/>'s Identifier
    /// property.</remarks>
    public class LaunchComplex: IncrementalCollectionObject, IEquatable<LaunchComplex>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public LaunchComplex(Guid id) :base(id)
        {
            HangarBay.Identifier = id;
        }

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

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new LaunchComplex(Id);
        }

        public bool Equals(LaunchComplex? other)
        {
            if (other == null || other.GetType() != typeof(LaunchComplex))
            {
                return false;
            }

            return base.Equals(other) &&
                Name == other.Name &&
                LaunchPads.SequenceEqual(other.LaunchPads) &&
                HangarBay.Equals(other.HangarBay);
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (LaunchComplex)fromObject;
            Name = from.Name;
            LaunchPads = from.LaunchPads.Select(lp => lp.DeepClone()).ToList();
            HangarBay = from.HangarBay.DeepClone();
        }
    }
}
