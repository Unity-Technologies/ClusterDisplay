using System;
// ReSharper disable once RedundantUsingDirective -> Need when compiling in Unity
using System.Collections.Generic;
// ReSharper disable once RedundantUsingDirective -> Need when compiling in Unity
using System.Linq;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Class used to store the data for an incremental update.
    /// </summary>
    /// <typeparam name="T">Type of objects in the collection</typeparam>
    public class IncrementalCollectionUpdate<T> where T : IIncrementalCollectionObject
    {
        /// <summary>
        /// Updated or added objects
        /// </summary>
        // ReSharper disable once PropertyCanBeMadeInitOnly.Global -> Not supported by older versions of C# used by Unity
        public IEnumerable<T> UpdatedObjects { get; set; } = Enumerable.Empty<T>();

        /// <summary>
        /// Identifier of removed objects.
        /// </summary>
        // ReSharper disable once PropertyCanBeMadeInitOnly.Global -> Not supported by older versions of C# used by Unity
        public IEnumerable<Guid> RemovedObjects { get; set; } = Enumerable.Empty<Guid>();

        /// <summary>
        /// Version number of the next update.
        /// </summary>
        /// <remarks>The main intended use of this property is to be used by the part of code controlling when and how
        /// <see cref="IncrementalCollection{T}.GetDeltaSince"/> is called to know from which version to ask or the next
        /// update, it is not used by <see cref="IncrementalCollection{T}.ApplyDelta"/>.</remarks>
        // ReSharper disable once PropertyCanBeMadeInitOnly.Global -> Not supported by older versions of C# used by Unity
        public UInt64 NextUpdate { get; set; }

        /// <summary>
        /// Returns if the update does not contain anything of value (could be skipped and the result would be the same).
        /// </summary>
        public bool IsEmpty => !UpdatedObjects.Any() && !RemovedObjects.Any();
    }
}
