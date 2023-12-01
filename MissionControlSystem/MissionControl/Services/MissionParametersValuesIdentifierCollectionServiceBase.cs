namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    /// <summary>
    /// Service manging an <see cref="IncrementalCollection{T}"/> of objects exposing the
    /// <see cref="IWithMissionParameterValueIdentifier"/> interface.
    /// </summary>
    public class MissionParametersValuesIdentifierCollectionServiceBase<T>
        where T : IWithMissionParameterValueIdentifier, IIncrementalCollectionObject, IEquatable<T>
    {
        /// <summary>
        /// Returns the list of cloned objects of the collection of the service.
        /// </summary>
        public IEnumerable<T> CloneAll()
        {
            lock (m_Lock)
            {
                return m_Collection.Values.Select(v => v.DeepClone()).ToList();
            }
        }

        /// <summary>
        /// Returns the list of cloned objects of the collection of the service if something has changed since the
        /// specified version.
        /// </summary>
        /// <param name="versionNumber">Version number identifying the last known version.  Gets updated to the current
        /// version number if changes are detected and a list returned.</param>
        // ReSharper disable once MemberCanBeProtected.Global
        public IEnumerable<T>? CloneAllIfChanged(ref ulong versionNumber)
        {
            lock (m_Lock)
            {
                if (m_Collection.VersionNumber > versionNumber)
                {
                    versionNumber = m_Collection.VersionNumber;
                    return m_Collection.Values.Select(v => v.DeepClone()).ToList();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns a clone of the object with the specified identifier in the collection.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <exception cref="KeyNotFoundException">If <paramref name="id"/> is not found in the collection of values.
        /// </exception>
        public T CloneSingle(Guid id)
        {
            lock (m_Lock)
            {
                return m_Collection[id].DeepClone();
            }
        }

        /// <summary>
        /// Sets the specified object in the collection (adds a new one or change the value of an already existing one).
        /// </summary>
        /// <param name="toSet">The object to put in the service's collection.</param>
        /// <exception cref="ArgumentException">There is a problem with <see cref="toSet"/>.</exception>
        public void Set(T toSet)
        {
            if (string.IsNullOrEmpty(toSet.ValueIdentifier))
            {
                throw new ArgumentException("ValueIdentifier is mandatory.", nameof(toSet));
            }

            lock (m_Lock)
            {
                if (m_ValueIdentifierIndex.TryGetValue(toSet.ValueIdentifier, out var withSameValueIdentifier) &&
                    withSameValueIdentifier.Id != toSet.Id)
                {
                    throw new ArgumentException("Duplicated ValueIdentifier within the collection.", nameof(toSet));
                }

                if (m_Collection.TryGetValue(toSet.Id, out var previousValue))
                {
                    if (previousValue.ValueIdentifier != toSet.ValueIdentifier)
                    {
                        m_ValueIdentifierIndex.Remove(previousValue.ValueIdentifier);
                        m_ValueIdentifierIndex.Add(toSet.ValueIdentifier, previousValue);
                    }
                    if (!previousValue.Equals(toSet))
                    {
                        previousValue.DeepCopyFrom(toSet);
                        previousValue.SignalChanges(m_Collection);
                    }
                }
                else
                {
                    var clone = toSet.DeepClone();
                    m_Collection[toSet.Id] = clone;
                    m_ValueIdentifierIndex[toSet.ValueIdentifier] = clone;
                }
            }
        }

        /// <summary>
        /// Set the content of the collection to the given list of values
        /// </summary>
        /// <param name="newListOfObjects">New list of objects to set the collection with.</param>
        public void SetAll(IEnumerable<T> newListOfObjects)
        {
            // First let's validate the new list of objects is valid (no value identifier conflicts).
            HashSet<string> valueIdentifiers = new();
            foreach (T newObject in newListOfObjects)
            {
                if (!valueIdentifiers.Add(newObject.ValueIdentifier))
                {
                    throw new ArgumentException($"Two or more objects have the value identifier " +
                        $"{newObject.ValueIdentifier}.", nameof(newListOfObjects));
                }
            }

            // Now we know it will succeed, let's do the work.
            lock (m_Lock)
            {
                m_Collection.Clear();
                m_ValueIdentifierIndex.Clear();

                IncrementalCollectionUpdate<T> initialUpdate = new() { UpdatedObjects = newListOfObjects };
                m_Collection.ApplyDelta(initialUpdate);

                foreach (T fromCollection in m_Collection.Values)
                {
                    m_ValueIdentifierIndex.Add(fromCollection.ValueIdentifier, fromCollection);
                }
            }
        }

        /// <summary>
        /// Removes the object with the specified identifier from the collection.
        /// </summary>
        /// <param name="id">Object's identifier.</param>
        /// <exception cref="KeyNotFoundException">If <paramref name="id"/> is not found in the collection of values.
        /// </exception>
        public void Delete(Guid id)
        {
            lock (m_Lock)
            {
                if (!m_Collection.TryGetValue(id, out var value))
                {
                    throw new KeyNotFoundException($"Cannot find MissionParameterValue {id}.");
                }
                m_ValueIdentifierIndex.Remove(value.ValueIdentifier);
                m_Collection.Remove(id);
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="currentMissionLaunchConfigurationService">Service into which to register the
        /// <see cref="IncrementalCollection{T}"/>.</param>
        protected MissionParametersValuesIdentifierCollectionServiceBase(
            CurrentMissionLaunchConfigurationService currentMissionLaunchConfigurationService)
        {
            using var lockedLaunchConfiguration = currentMissionLaunchConfigurationService.LockAsync().Result;
            m_LastCurrentMissionLaunchConfigurationAssetId = lockedLaunchConfiguration.Value.AssetId;
            lockedLaunchConfiguration.Value.ObjectChanged += CurrentMissionLaunchConfigurationChanged;
        }

        /// <summary>
        /// Register the collection of this service into the <see cref="IncrementalCollectionCatalogService"/>.
        /// </summary>
        /// <param name="incrementalCollectionCatalogService"></param>
        /// <param name="registerName"></param>
        protected void Register(IncrementalCollectionCatalogService incrementalCollectionCatalogService,
            string registerName)
        {
            incrementalCollectionCatalogService.Register(registerName, RegisterForChangesInCollection,
                GetIncrementalUpdatesAsync);
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to register callback to detect changes in the
        /// collection.
        /// </summary>
        /// <param name="toRegister">The callback to register</param>
        void RegisterForChangesInCollection(Action<IReadOnlyIncrementalCollection> toRegister)
        {
            lock (m_Lock)
            {
                m_Collection.SomethingChanged += toRegister;
            }
        }

        /// <summary>
        /// <see cref="IncrementalCollectionCatalogService"/>'s callback to get an incremental update from the specified
        /// version.
        /// </summary>
        /// <param name="fromVersion">Version number from which we want to get the incremental update.</param>
        Task<object?> GetIncrementalUpdatesAsync(ulong fromVersion)
        {
            lock (m_Lock)
            {
                var ret = m_Collection.GetDeltaSince(fromVersion);
                return Task.FromResult(ret.IsEmpty ? null : (object)ret);
            }
        }

        /// <summary>
        /// Delegate called when the current mission's launch configuration changes.
        /// </summary>
        /// <param name="obj"></param>
        void CurrentMissionLaunchConfigurationChanged(ObservableObject obj)
        {
            var launchConfiguration = (LaunchConfiguration)obj;

            lock (m_Lock)
            {
                if (launchConfiguration.AssetId != m_LastCurrentMissionLaunchConfigurationAssetId)
                {
                    m_LastCurrentMissionLaunchConfigurationAssetId = launchConfiguration.AssetId;
                    m_Collection.Clear();
                    m_ValueIdentifierIndex.Clear();
                }
            }
        }

        /// <summary>
        /// Synchronize access to member variables below
        /// </summary>
        object m_Lock = new();

        /// <summary>
        /// The <see cref="IncrementalCollection{T}"/> of <see cref="MissionParameterValue"/>.
        /// </summary>
        IncrementalCollection<T> m_Collection = new();

        /// <summary>
        /// Index ValueIdentifier of every object in <see cref="m_Collection"/>.
        /// </summary>
        Dictionary<string, T> m_ValueIdentifierIndex = new();

        /// <summary>
        /// Last known value of <see cref="LaunchConfiguration.AssetId"/>.
        /// </summary>
        Guid m_LastCurrentMissionLaunchConfigurationAssetId;
    }
}
