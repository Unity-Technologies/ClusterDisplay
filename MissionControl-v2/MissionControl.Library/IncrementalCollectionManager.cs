using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Base class for manager that are simply managing access to an <see cref="IncrementalCollection{T}"/>.
    /// </summary>
    public class IncrementalCollectionManager<T> where T : IIncrementalCollectionObject
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        protected IncrementalCollectionManager(ILogger logger)
        {
            m_Logger = logger;

            m_Collection.SomethingChanged += CollectionModified;
        }

        /// <summary>
        /// Save the state of the <see cref="IncrementalCollectionManager{T}"/> to the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="saveTo"><see cref="Stream"/> to save to.</param>
        public async Task SaveAsync(Stream saveTo)
        {
            // Remark: Double buffering to a temporary MemoryStream to avoid keeping m_Lock locked for too long if
            // saveTo is slow for whatever reason.
            await using MemoryStream inMemorySerialized = new();

            using (await m_Lock.LockAsync())
            {
                await JsonSerializer.SerializeAsync(inMemorySerialized, m_Collection.Values, Json.SerializerOptions);
            }
            inMemorySerialized.Position = 0;

            await inMemorySerialized.CopyToAsync(saveTo);
        }

        /// <summary>
        /// Loads the state of the <see cref="IncrementalCollectionManager{T}"/> from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="loadFrom"><see cref="Stream"/> to load from.</param>
        /// <exception cref="InvalidOperationException">If trying to load into a non empty manager.</exception>
        public void Load(Stream loadFrom)
        {
            var objects = JsonSerializer.Deserialize<T[]>(loadFrom, Json.SerializerOptions);
            if (objects == null)
            {
                throw new NullReferenceException($"Parsing {nameof(T)} array resulted in a null object.");
            }

            // Prepare an incremental update (this is the fastest way to add everything as a single transaction into
            // m_Collection).
            IncrementalCollectionUpdate<T> incrementalUpdate = new() { UpdatedObjects = objects };

            using (m_Lock.Lock())
            {
                if (m_Collection.Count > 0)
                {
                    throw new InvalidOperationException($"Can only call Load on an empty {nameof(T)}.");
                }

                try
                {
                    Debug.Assert(!m_CollectionModificationInProgress);
                    m_CollectionModificationInProgress = true;
                    m_Collection.ApplyDelta(incrementalUpdate);
                }
                finally
                {
                    m_CollectionModificationInProgress = false;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="AsyncLockedObject{T}"/> giving access to a
        /// <see cref="IReadOnlyIncrementalCollection{T}"/> that must be disposed ASAP (as it keeps the
        /// <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        public async Task<AsyncLockedObject<IReadOnlyIncrementalCollection<T>>> GetLockedReadOnlyAsync()
        {
            return new AsyncLockedObject<IReadOnlyIncrementalCollection<T>>(m_Collection,
                await m_Lock.LockAsync());
        }

        /// <summary>
        /// Returns a <see cref="WriteLock"/> giving a write access to the managed <see cref="IncrementalCollection{T}"/>
        /// that must be disposed ASAP (as it keeps the <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        protected async Task<WriteLock> GetWriteLockAsync()
        {
            IDisposable locked = await m_Lock.LockAsync();
            Debug.Assert(!m_CollectionModificationInProgress);
            m_CollectionModificationInProgress = true;
            return new WriteLock(m_Collection, locked, () => m_CollectionModificationInProgress = false);
        }

        /// <summary>
        /// Logger to be used to send messages.
        /// </summary>
        protected ILogger Logger => m_Logger;

        /// <summary>
        /// Small object giving a write access to the managed <see cref="IncrementalCollection{T}"/> and the objects it
        /// contains.
        /// </summary>
        /// <remarks>It keeps the <see cref="IncrementalCollectionManager{T}"/> locked, so dispose as soon as you do not
        /// need to modify the collection or its objects anymore.</remarks>
        protected class WriteLock: IDisposable
        {
            public WriteLock(IncrementalCollection<T> incrementalCollection, IDisposable lockDisposable, Action disposedOf)
            {
                m_IncrementalCollection = incrementalCollection;
                m_LockDisposable = lockDisposable;
                m_DisposedOf = disposedOf;
            }

            /// <summary>
            /// Returns access to the collection we are giving a write access to.
            /// </summary>
            public IncrementalCollection<T> Collection
            {
                get
                {
                    if (m_IncrementalCollection == null)
                    {
                        throw new ObjectDisposedException(nameof(WriteLock));
                    }
                    return m_IncrementalCollection;
                }
            }

            public void Dispose()
            {
                m_DisposedOf?.Invoke();
                m_DisposedOf = null;
                m_LockDisposable?.Dispose();
                m_LockDisposable = null;
                m_IncrementalCollection = null;
            }

            /// <summary>
            /// The collection we represent a write lock for
            /// </summary>
            IncrementalCollection<T>? m_IncrementalCollection;

            /// <summary>
            /// The actual lock to the collection to dispose of when we are disposed of.
            /// </summary>
            IDisposable? m_LockDisposable;

            /// <summary>
            /// <see cref="Action"/> to call when we are disposed of.
            /// </summary>
            Action? m_DisposedOf;
        }

        /// <summary>
        /// Watchdog to detect if the collection would be modified when not supposed to.
        /// </summary>
        /// <remarks>This is not meant to prevent problems, this is meant to reduce chances it goes unnoticed.</remarks>
        void CollectionModified(IReadOnlyIncrementalCollection _)
        {
            if (!m_Lock.IsLocked)
            {
                throw new InvalidOperationException($"Modifying {nameof(IncrementalCollection<T>)} while the " +
                    $"collection is not locked.");
            }
            if (!m_CollectionModificationInProgress)
            {
                throw new InvalidOperationException($"Looks likes {nameof(IncrementalCollection<T>)} from the " +
                    $"{nameof(IReadOnlyIncrementalCollection<T>)}.");
            }
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;

        /// <summary>
        /// Object used to synchronize access to the member variables below.
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// The collection
        /// </summary>
        readonly IncrementalCollection<T> m_Collection = new();

        /// <summary>
        /// Are we currently modifying m_Collection?  The main goal of this variable is to detect external code that
        /// would be modifying objects that we are supposed to be the only ones to modify.
        /// </summary>
        bool m_CollectionModificationInProgress;
    }
}
