using System.Diagnostics;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class IncrementalCollectionServiceExtension
    {
        public static void AddIncrementalCollectionCatalogService(this IServiceCollection services)
        {
            services.AddSingleton<IncrementalCollectionCatalogService>();
        }
    }

    /// <summary>
    /// Service into which every <see cref="IncrementalCollection{T}"/> that want to be exposed through the
    /// <see cref="ClusterDisplay.MissionControl.MissionControl.Controllers.IncrementalCollectionsUpdateController"/>
    /// must register.
    /// </summary>
    public class IncrementalCollectionCatalogService
    {
        public IncrementalCollectionCatalogService(ILogger<IncrementalCollectionCatalogService> logger)
        {
            m_Logger = logger;
        }

        /// <summary>
        /// Register an <see cref="IncrementalCollection{T}"/> to provide incremental updates from a version.
        /// </summary>
        /// <param name="name">Name of the <see cref="IncrementalCollection{T}"/>.</param>
        /// <param name="collection">The <see cref="IncrementalCollection{T}"/> (as an interface without generics).
        /// </param>
        /// <param name="callback">Callback that should produce a <see cref="IncrementalCollectionUpdate{T}"/> that
        /// contains the incremental update from the specified version (the <see cref="ulong"/> parameter) to the
        /// current state.  It should return null if no update is currently available.</param>
        /// <exception cref="ArgumentException">There is already a <see cref="IncrementalCollection{T}"/> registered
        /// with that <paramref name="name"/>.</exception>
        /// <remarks>There is current no unregister method.  Could probably be done but we currently don't need it so
        /// let's keep the code as simple as possible.</remarks>
        public void Register(string name, IReadOnlyIncrementalCollection collection, Func<ulong, Task<object?>> callback)
        {
            lock (m_Lock)
            {
                if (!m_Registry.ContainsKey(name))
                {
                    CollectionData newData = new(collection, callback);
                    m_Registry.Add(name, newData);
                }
                else
                {
                    throw new ArgumentException($"There is already a collection named {name} registered.");
                }
            }
        }

        /// <summary>
        /// Returns a task that will complete when an <see cref="IncrementalCollectionUpdate{T}"/> for the specified
        /// collection can be produced from the given version number.
        /// </summary>
        /// <param name="collectionName">Collection name.</param>
        /// <param name="versionNumber">Version number from which we want to get an incremental update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="KeyNotFoundException">If no collection with the specified <paramref name="collectionName"/>
        /// can be found.</exception>
        public async Task<object> GetIncrementalUpdateFromAsync(string collectionName, ulong versionNumber,
            CancellationToken cancellationToken)
        {
            CollectionData collectionData;
            lock (m_Lock)
            {
                collectionData = m_Registry[collectionName];
            }

            for (; ; )
            {
                // Get the task that will get signaled when something changes.  Important, get it before asking for the
                // incremental update or otherwise we could end up waiting for nothing because of a race condition
                // between the waiting task and having an empty update or not.
                Task waitTask = collectionData.GetSomethingChangedTask();

                object? ret = await collectionData.Callback(versionNumber);
                if (ret != null)
                {
                    return ret;
                }

                await waitTask.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Information about registered <see cref="IncrementalCollection{T}"/>.
        /// </summary>
        class CollectionData
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="collection">The <see cref="IncrementalCollection{T}"/> (as an interface without generics).
            /// </param>
            /// <param name="callback">Callback that should produce a <see cref="IncrementalCollectionUpdate{T}"/> that
            /// contains the incremental update from the specified version (the <see cref="ulong"/> parameter) to the
            /// current state.  It should return null if no update is currently available.</param>
            public CollectionData(IReadOnlyIncrementalCollection collection, Func<ulong, Task<object?>> callback)
            {
                Callback = callback;
                m_Collection = collection;

                m_Collection.SomethingChanged += SomethingChangedInCollection;
            }

            /// <summary>
            /// Returns a task that will complete when something changes in the collection we are monitoring.
            /// </summary>
            /// <returns></returns>
            public Task GetSomethingChangedTask()
            {
                lock (m_Lock)
                {
                    m_SomethingChangedTaskCompletionSource ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    return m_SomethingChangedTaskCompletionSource.Task;
                }
            }

            /// <summary>
            /// Callback that should produce a <see cref="IncrementalCollectionUpdate{T}"/> that contains the
            /// incremental update from the specified version (the <see cref="ulong"/> parameter) to the current state.
            /// It should return null if no update is currently available.
            /// </summary>
            public Func<ulong, Task<object?>> Callback { get; }

            /// <summary>
            /// Callback called when something changes in <see cref="m_Collection"/>.
            /// </summary>
            /// <param name="changed">The collection that changed.</param>
            void SomethingChangedInCollection(IReadOnlyIncrementalCollection changed)
            {
                Debug.Assert(ReferenceEquals(m_Collection, changed));
                lock (m_Lock)
                {
                    m_SomethingChangedTaskCompletionSource?.TrySetResult();
                    m_SomethingChangedTaskCompletionSource = null;
                }
            }

            /// <summary>
            /// The <see cref="IncrementalCollection{T}"/> (as an interface without generics).
            /// </summary>
            IReadOnlyIncrementalCollection m_Collection;

            /// <summary>
            /// Object used to synchronize access to the member variables below.
            /// </summary>
            readonly object m_Lock = new();

            /// <summary>
            /// Stores information about every registered <see cref="IncrementalCollection{T}"/>.
            /// </summary>
            TaskCompletionSource? m_SomethingChangedTaskCompletionSource;
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;

        /// <summary>
        /// Object used to synchronize access to the member variables below.
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// Stores information about every registered <see cref="IncrementalCollection{T}"/>.
        /// </summary>
        readonly Dictionary<string, CollectionData> m_Registry = new();
    }
}
