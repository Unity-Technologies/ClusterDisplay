using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Represent the external view of a MissionControl's Asset.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class Asset: IIncrementalCollectionObject, IEquatable<Asset>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object.</param>
        public Asset(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Identifier of the object
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// <see cref="Launchable"/>s composing this asset.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global -> Set needed as public by Json serialisation
        public List<Launchable> Launchables { get; set; } = new();

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (Asset)fromObject;
            Launchables = from.Launchables.Select(l => l.DeepClone()).ToList();
        }

        public bool Equals(Asset other)
        {
            return other != null &&
                Id == other.Id &&
                Launchables.SequenceEqual(other.Launchables);
        }
    }
}
