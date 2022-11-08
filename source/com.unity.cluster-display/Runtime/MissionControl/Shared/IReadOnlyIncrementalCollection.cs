using System;
// ReSharper disable once RedundantUsingDirective -> Need when compiling in Unity
using System.Collections.Generic;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Read only interface on IncrementalCollection.
    /// </summary>
    public interface IReadOnlyIncrementalCollection
    {
        /// <summary>
        /// Event called to inform that something in the collection changed (merge of
        /// <see cref="IReadOnlyIncrementalCollection{T}.ObjectAdded"/>,
        /// <see cref="IReadOnlyIncrementalCollection{T}.ObjectRemoved"/> and
        /// <see cref="IReadOnlyIncrementalCollection{T}.ObjectUpdated"/> without any generic parameter).
        /// </summary>
#if UNITY_64
        public event Action<IReadOnlyIncrementalCollection> SomethingChanged;
#else
        public event Action<IReadOnlyIncrementalCollection>? SomethingChanged;
#endif
    }

    /// <summary>
    /// Read only generic interface on IncrementalCollection.
    /// </summary>
    /// <typeparam name="T">Type of objects in the collection.</typeparam>
    public interface IReadOnlyIncrementalCollection<T>
        : IReadOnlyDictionary<Guid, T>
        , IReadOnlyIncrementalCollection
        where T : IIncrementalCollectionObject
    {
        /// <summary>
        /// Event called to inform that an object has been added to the collection.
        /// </summary>
        /// <remarks>This event is called when add is completed (last thing before the method that added the object
        /// returns).</remarks>
#if UNITY_64
        public event Action<T> ObjectAdded;
#else
        public event Action<T>? ObjectAdded;
#endif

        /// <summary>
        /// Event called to inform that an object has been removed from the collection.
        /// </summary>
        /// <remarks>This event is called when remove is completed (last thing before the method that removed the object
        /// returns).</remarks>
#if UNITY_64
        public event Action<T> ObjectRemoved;
#else
        public event Action<T>? ObjectRemoved;
#endif

        /// <summary>
        /// Event called to inform that the specified object has been modified.
        /// </summary>
#if UNITY_64
        public event Action<T> ObjectUpdated;
#else
        public event Action<T>? ObjectUpdated;
#endif

        /// <summary>
        /// Compute what changed since the reference version number.
        /// </summary>
        /// <param name="sinceVersionNumber">Returns all the changes for which VersionNumber >= sinceVersionNumber</param>
        public IncrementalCollectionUpdate<T> GetDeltaSince(UInt64 sinceVersionNumber);
    }
}
