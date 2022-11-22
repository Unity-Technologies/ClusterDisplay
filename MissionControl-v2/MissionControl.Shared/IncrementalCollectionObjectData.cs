using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Data about objects in an <see cref="IncrementalCollection{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of objects in the collection.</typeparam>
    class IncrementalCollectionObjectData<T> where T: IIncrementalCollectionObject
    {
        /// <summary>
        /// The object of the collection we store additional data about.
        /// </summary>
        public T? Object { get; set; }

        /// <summary>
        /// Version number indicating when was this object modified with relation to the other ones in the owning
        /// collection.
        /// </summary>
        public ulong VersionNumber { get; set; }

        /// <summary>
        /// First VersionNumber of the object when it was added to the owning collection.
        /// </summary>
        public ulong FirstVersionNumber { get; set; }
    }
}
