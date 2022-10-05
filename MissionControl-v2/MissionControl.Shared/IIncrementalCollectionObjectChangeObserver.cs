using System;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Interface used by <see cref="IncrementalCollectionObject"/> to notify an owning
    /// <see cref="IncrementalCollection{T}"/> that it changed.
    /// </summary>
    interface IIncrementalCollectionObjectChangeObserver
    {
        /// <summary>
        /// Method called by the <see cref="IncrementalCollectionObject"/> to signal it changed.
        /// </summary>
        /// <param name="obj">The <see cref="IncrementalCollectionObject"/>.</param>
        /// <remarks>Implementation of the method should update <paramref name="obj"/>'s
        /// <see cref="IncrementalCollectionObject.VersionNumber"/>.</remarks>
        void ObjectChanged(IncrementalCollectionObject obj);
    }
}
