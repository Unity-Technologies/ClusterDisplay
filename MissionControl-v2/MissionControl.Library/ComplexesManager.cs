using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Class of the object responsible for managing <see cref="LaunchComplex"/>.
    /// </summary>
    /// <remarks>I must admit, this class does not do much and could probably have been done all in the service, however
    /// having it split in a different class like this one allow it to be more similar to <see cref="AssetsManager"/>,
    /// easier to test and ready to do more work in the future if <see cref="LaunchComplex"/> management requires more
    /// work to be done.</remarks>
    public class ComplexesManager
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        public ComplexesManager(ILogger logger)
        {
            m_Logger = logger;

            m_Collection.OnSomethingChanged += CollectionModified;
        }

        /// <summary>
        /// Add / modify a <see cref="LaunchComplex"/>.
        /// </summary>
        /// <param name="complex">New / updated <see cref="LaunchComplex"/>.</param>
        /// <exception cref="ArgumentException">Something bad found in <paramref name="complex"/>.</exception>
        public void Put(LaunchComplex complex)
        {
            if (complex.HangarBay.Identifier != complex.Id)
            {
                throw new ArgumentException("complex.HangarBay.Identifier != complex.Id");
            }

            HashSet<Guid> identifierSet = new();
            HashSet<Uri> endpointSet = new();
            identifierSet.Add(complex.Id);
            endpointSet.Add(complex.HangarBay.Endpoint);
            foreach (var launchPad in complex.LaunchPads)
            {
                if (!identifierSet.Add(launchPad.Identifier))
                {
                    throw new ArgumentException($"{nameof(LaunchPad)} {launchPad.Identifier} has the same identifier " +
                        $"than another {nameof(LaunchPad)} or the {nameof(HangarBay)}.");
                }
                if (!endpointSet.Add(launchPad.Endpoint))
                {
                    throw new ArgumentException($"{nameof(LaunchPad)} {launchPad.Identifier} has the same endpoint " +
                        $"({launchPad.Endpoint}) than another {nameof(LaunchPad)} or the {nameof(HangarBay)}.");
                }
            }

            using (m_Lock.Lock())
            {
                try
                {
                    // Before doing the modification, each complex added should be unique and point to a resource that
                    // is not already present in the list...
                    foreach (var currentComplex in m_Collection.Values)
                    {
                        if (currentComplex.Id == complex.Id)
                        {
                            continue;
                        }

                        if (identifierSet.Contains(currentComplex.HangarBay.Identifier))
                        {
                            throw new ArgumentException($"Identifier {currentComplex.HangarBay.Identifier} is " +
                                $"already used by another {nameof(LaunchComplex)}.");
                        }
                        if (endpointSet.Contains(currentComplex.HangarBay.Endpoint))
                        {
                            throw new ArgumentException($"Endpoint {currentComplex.HangarBay.Endpoint} is already " +
                                $"used by another {nameof(LaunchComplex)}.");
                        }

                        foreach (var launchpad in currentComplex.LaunchPads)
                        {
                            if (identifierSet.Contains(launchpad.Identifier))
                            {
                                throw new ArgumentException($"Identifier {launchpad.Identifier} is already used by " +
                                    $"another {nameof(LaunchComplex)}.");
                            }
                            if (endpointSet.Contains(launchpad.Endpoint))
                            {
                                throw new ArgumentException($"Endpoint {launchpad.Endpoint} is already used by " +
                                    $"another {nameof(LaunchComplex)}.");
                            }
                        }
                    }

                    // Now that we know everything is ok let's do the modifications
                    Debug.Assert(!m_CollectionModificationAllowed);
                    m_CollectionModificationAllowed = true;
                    m_Collection[complex.Id] = complex;
                }
                finally
                {
                    m_CollectionModificationAllowed = false;
                }
            }
        }

        /// <summary>
        /// Remove the <see cref="LaunchComplex"/> with the specified identifier.
        /// </summary>
        /// <param name="identifier"><see cref="LaunchComplex"/>'s identifier.</param>
        /// <returns><c>true</c> if remove succeeded or <c>false</c> if there was no asset with that identifier in the
        /// list of <see cref="Asset"/>s of the <see cref="AssetsManager"/>.  Any other problem removing will throw an
        /// exception.</returns>
        public bool Remove(Guid identifier)
        {
            // First get the asset and remove it from the "known list" so that we do not have to keep m_Lock locked for
            // the whole removal process and so that it appear to be gone ASAP from the outside.
            using (m_Lock.Lock())
            {
                try
                {
                    Debug.Assert(!m_CollectionModificationAllowed);
                    m_CollectionModificationAllowed = true;
                    return m_Collection.Remove(identifier);
                }
                finally
                {
                    m_CollectionModificationAllowed = false;
                }
            }
        }

        /// <summary>
        /// Save the state of the <see cref="ComplexesManager"/> to the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="saveTo"><see cref="Stream"/> to save to.</param>
        public async Task SaveAsync(Stream saveTo)
        {
            await using MemoryStream inMemorySerialized = new();

            using (await m_Lock.LockAsync())
            {
                await JsonSerializer.SerializeAsync(inMemorySerialized, m_Collection.Values, Json.SerializerOptions);
            }
            inMemorySerialized.Position = 0;

            await inMemorySerialized.CopyToAsync(saveTo);
        }

        /// <summary>
        /// Loads the state of the <see cref="ComplexesManager"/> from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="loadFrom"><see cref="Stream"/> to load from.</param>
        /// <exception cref="InvalidOperationException">If trying to load into a non empty manager.</exception>
        public void Load(Stream loadFrom)
        {
            var objects = JsonSerializer.Deserialize<LaunchComplex[]>(loadFrom, Json.SerializerOptions);
            if (objects == null)
            {
                throw new NullReferenceException("Parsing launch complex array resulted in a null object.");
            }

            // Prepare an incremental update (this is the fastest way to add everything as a single transaction into
            // m_Collection).
            IncrementalCollectionUpdate<LaunchComplex> incrementalUpdate = new();
            incrementalUpdate.UpdatedObjects = objects;

            using (m_Lock.Lock())
            {
                if (m_Collection.Count > 0)
                {
                    throw new InvalidOperationException($"Can only call Load on an empty {nameof(ComplexesManager)}.");
                }

                try
                {
                    Debug.Assert(!m_CollectionModificationAllowed);
                    m_CollectionModificationAllowed = true;
                    m_Collection.ApplyDelta(incrementalUpdate);
                }
                finally
                {
                    m_CollectionModificationAllowed = false;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="AsyncLockedObject{T}"/> giving access to a
        /// <see cref="IReadOnlyIncrementalCollection{T}"/> that must be disposed ASAP (as it keeps the
        /// <see cref="IncrementalCollection{T}"/> locked for other threads).
        /// </summary>
        public async Task<AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchComplex>>> GetLockedReadOnlyAsync()
        {
            return new AsyncLockedObject<IReadOnlyIncrementalCollection<LaunchComplex>>(m_Collection,
                await m_Lock.LockAsync());
        }

        /// <summary>
        /// Watchdog to detect if the collection would be modified when not supposed to.
        /// </summary>
        /// <remarks>This is not meant to prevent problems, this is meant to reduce chances it goes unnoticed.</remarks>
        void CollectionModified(IReadOnlyIncrementalCollection _)
        {
            if (!m_Lock.IsLocked)
            {
                throw new InvalidOperationException($"Modifying {nameof(IncrementalCollection<LaunchComplex>)} while " +
                    $"the collection is not locked.");
            }
            if (!m_CollectionModificationAllowed)
            {
                throw new InvalidOperationException($"Looks likes {nameof(IncrementalCollection<LaunchComplex>)} " +
                    $"from the {nameof(IReadOnlyIncrementalCollection<LaunchComplex>)}.");
            }
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;

        /// <summary>
        /// Object used to synchronize access to the member variables below.
        /// </summary>
        readonly AsyncLock m_Lock = new();

        /// <summary>
        /// Assets collection
        /// </summary>
        readonly IncrementalCollection<LaunchComplex> m_Collection = new();

        /// <summary>
        /// Assuming <see cref="m_Lock"/> is locked by the current thread, is the current thread allowed to modify it?
        /// </summary>
        bool m_CollectionModificationAllowed;
    }
}
