using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Read only interface on IncrementalCollection.
    /// </summary>
    public interface IReadOnlyIncrementalCollection
    {
        /// <summary>
        /// Event called to inform that something in the collection changed (merge of
        /// <see cref="IReadOnlyIncrementalCollection{T}.OnObjectAdded"/>,
        /// <see cref="IReadOnlyIncrementalCollection{T}.OnObjectRemoved"/> and
        /// <see cref="IReadOnlyIncrementalCollection{T}.OnObjectUpdated"/> without any generic parameter).
        /// </summary>
        public event Action<IReadOnlyIncrementalCollection>? OnSomethingChanged;
    }

    /// <summary>
    /// Read only generic interface on IncrementalCollection.
    /// </summary>
    /// <typeparam name="T">Type of objects in the collection.</typeparam>
    public interface IReadOnlyIncrementalCollection<T>
        : IReadOnlyDictionary<Guid, T>
        , IReadOnlyIncrementalCollection
        where T : IncrementalCollectionObject
    {
        /// <summary>
        /// Event called to inform that an object has been added to the collection.
        /// </summary>
        /// <remarks>This event is called when add is completed (last thing before the method that added the object
        /// returns).</remarks>
        public event Action<T>? OnObjectAdded;

        /// <summary>
        /// Event called to inform that an object has been removed from the collection.
        /// </summary>
        /// <remarks>This event is called when remove is completed (last thing before the method that removed the object
        /// returns).</remarks>
        public event Action<T>? OnObjectRemoved;

        /// <summary>
        /// Event called to inform that the specified object has been modified.
        /// </summary>
        public event Action<T>? OnObjectUpdated;

        /// <summary>
        /// Compute what changed since the reference version number.
        /// </summary>
        /// <param name="sinceVersionNumber">Returns all the changes for which VersionNumber >= sinceVersionNumber</param>
        public IncrementalCollectionUpdate<T> GetDeltaSince(UInt64 sinceVersionNumber);
    }
}
