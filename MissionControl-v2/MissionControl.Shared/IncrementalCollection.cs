using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Collection of objects where modifications are tracked and modifications since a given version can be computed.
    /// </summary>
    /// <typeparam name="T">Type of objects in the collection.</typeparam>
    public class IncrementalCollection<T>
        : IReadOnlyIncrementalCollection<T>
        , IDictionary<Guid, T>
        , IDictionary
        where T : IIncrementalCollectionObject
    {
        /// <summary>
        /// Event called to inform that an object has been added to the collection.
        /// </summary>
        /// <remarks>This event is called when add is completed (last thing before the method that added the object
        /// returns).</remarks>
        public event Action<T>? ObjectAdded;

        /// <summary>
        /// Event called to inform that an object has been removed from the collection.
        /// </summary>
        /// <remarks>This event is called when remove is completed (last thing before the method that removed the object
        /// returns).</remarks>
        public event Action<T>? ObjectRemoved;

        /// <summary>
        /// Event called to inform that the specified object has been modified.
        /// </summary>
        public event Action<T>? ObjectUpdated;

        /// <summary>
        /// Event called to inform that something in the collection changed (merge of <see cref="ObjectAdded"/>,
        /// <see cref="ObjectRemoved"/> and <see cref="ObjectUpdated"/> without any generic parameter).
        /// </summary>
        public event Action<IReadOnlyIncrementalCollection>? SomethingChanged;

        /// <summary>
        /// Add a <typeparamref name="T"/> to the collection (similar to
        /// <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/> but with one less parameter).
        /// </summary>
        /// <param name="obj">The <typeparamref name="T"/> to add to the collection.</param>
        public void Add(T obj)
        {
            ulong newVersionNumber = m_VersionNumber + 1;

            m_PublicCollection.Add(obj.Id, obj);
            if (m_InternalCollection.TryGetValue(obj.Id, out var objectData))
            {
                Debug.Assert(objectData.Object == null);
            }
            else
            {
                objectData = new() { FirstVersionNumber = newVersionNumber };
                m_InternalCollection[obj.Id] = objectData;
            }
            objectData.Object = obj;
            m_PublicCollection[obj.Id] = obj;
            objectData.VersionNumber = newVersionNumber;
            m_VersionNumber = newVersionNumber;

            ObjectAdded?.Invoke(obj);
            SomethingChanged?.Invoke(this);
        }

        public T this[Guid key]
        {
            get => m_PublicCollection[key];
            set
            {
                ValidateKeyMatchesValue(key, value);
                ulong newVersionNumber = m_VersionNumber + 1;
                bool added = true;
                if (m_InternalCollection.TryGetValue(key, out var objectData))
                {
                    added = objectData.Object == null;
                }
                else
                {
                    objectData = new() { FirstVersionNumber = newVersionNumber };
                    m_InternalCollection[key] = objectData;
                }
                objectData.Object = value;
                m_PublicCollection[key] = value;
                objectData.VersionNumber = newVersionNumber;
                m_VersionNumber = newVersionNumber;

                if (added)
                {
                    ObjectAdded?.Invoke(value);
                }
                else
                {
                    ObjectUpdated?.Invoke(value);
                }
                SomethingChanged?.Invoke(this);
            }
        }

        public ICollection<Guid> Keys => m_PublicCollection.Keys;

        public ICollection<T> Values => m_PublicCollection.Values;

        public int Count => m_PublicCollection.Count;

        public bool IsReadOnly => false;

        public void Add(Guid key, T value)
        {
            ValidateKeyMatchesValue(key, value);
            Add(value);
        }

        public void Clear()
        {
            if (Count == 0)
            {   // Already cleared
                return;
            }

            ulong newVersionNumber = m_VersionNumber + 1;
            foreach (var pair in m_PublicCollection)
            {
                var objectData = m_InternalCollection[pair.Key];
                objectData.Object = default(T);
                objectData.VersionNumber = newVersionNumber;
            }

            var oldDictionary = m_PublicCollection;
            m_PublicCollection = new();

            m_VersionNumber = newVersionNumber;

            foreach (var removedObject in oldDictionary.Values)
            {
                ObjectRemoved?.Invoke(removedObject);
            }
            SomethingChanged?.Invoke(this);
        }

        public bool ContainsKey(Guid key)
        {
            return m_PublicCollection.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<Guid, T>> GetEnumerator()
        {
            return m_PublicCollection.GetEnumerator();
        }

        public bool Remove(Guid key)
        {
            if (m_PublicCollection.TryGetValue(key, out T? removedObject))
            {
                bool removed = m_PublicCollection.Remove(key);
                Debug.Assert(removed);

                ulong newVersionNumber = m_VersionNumber + 1;
                var objectData = m_InternalCollection[key];
                objectData.Object = default(T);
                objectData.VersionNumber = newVersionNumber;
                m_VersionNumber = newVersionNumber;

                ObjectRemoved?.Invoke(removedObject);
                SomethingChanged?.Invoke(this);

                return true;
            }
            return false;
        }

        public bool TryGetValue(Guid key, [MaybeNullWhen(false)] out T value)
        {
            return m_PublicCollection.TryGetValue(key, out value);
        }

        /// <summary>
        /// Method to be called by anyone modifying properties of an object of the collection.
        /// </summary>
        /// <param name="obj">The modified object</param>
        /// <remarks>To be called after the modifications have been done.
        /// <br/><br/>Many changes on a single object can be batched before calling this method to signal them all in a
        /// single call.</remarks>
        public void SignalObjectChanged(T obj)
        {
            var objectData = m_InternalCollection[obj.Id];
            if (!ReferenceEquals(objectData.Object, obj))
            {
                return;
            }

            ++m_VersionNumber;
            objectData.VersionNumber = m_VersionNumber;

            ObjectUpdated?.Invoke(obj);
            SomethingChanged?.Invoke(this);
        }

        /// <summary>
        /// Version number of the collection (in increments every time something changes in the collection).
        /// </summary>
        public ulong VersionNumber => m_VersionNumber;

        /// <summary>
        /// Compute what changed since the reference version number.
        /// </summary>
        /// <param name="sinceVersionNumber">Returns all the changes for which VersionNumber >= sinceVersionNumber
        /// </param>
        public IncrementalCollectionUpdate<T> GetDeltaSince(UInt64 sinceVersionNumber)
        {
            var updatedObjects = new List<T>();
            var removedObjects = new List<Guid>();
            foreach (var (key, objectData) in m_InternalCollection)
            {
                if (objectData.VersionNumber >= sinceVersionNumber)
                {
                    if (objectData.Object != null)
                    {
                        T clone = objectData.Object.DeepClone();
                        updatedObjects.Add(clone);
                    }
                    else if (objectData.FirstVersionNumber < sinceVersionNumber)
                    {
                        removedObjects.Add(key);
                    }
                }
            }

            IncrementalCollectionUpdate<T> ret = new()
            {
                UpdatedObjects = updatedObjects.AsEnumerable(),
                RemovedObjects = removedObjects.AsEnumerable(),
                NextUpdate = VersionNumber + 1
            };
            return ret;
        }

        /// <summary>
        /// Apply all the changes to the objects of the collection so that it matches the content of another "source
        /// collection".
        /// </summary>
        /// <param name="update">The incremental update to apply.</param>
        /// <remarks>The <see cref="IncrementalCollectionUpdate{T}.NextUpdate"/> property of the
        /// <paramref name="update"/>  is not used.</remarks>
        public void ApplyDelta(IncrementalCollectionUpdate<T> update)
        {
            ulong newVersionNumber = VersionNumber + 1;

            List<(T obj, bool added)> updatedObjects = new (update.UpdatedObjects.Count());
            foreach (T updatedObject in update.UpdatedObjects)
            {
                if (m_InternalCollection.TryGetValue(updatedObject.Id, out var objectData) &&
                    objectData.Object != null)
                {
                    objectData.Object.DeepCopyFrom(updatedObject);
                    updatedObjects.Add((objectData.Object, false));
                }
                else
                {
                    if (objectData == null)
                    {
                        objectData = new() { FirstVersionNumber = newVersionNumber };
                        m_InternalCollection[updatedObject.Id] = objectData;
                    }

                    objectData.Object = updatedObject.DeepClone();
                    m_PublicCollection[updatedObject.Id] = objectData.Object;
                    updatedObjects.Add((objectData.Object, true));

                }
                objectData.VersionNumber = newVersionNumber;
            }

            List<T> removedObjects = new (update.RemovedObjects.Count());
            foreach (Guid removedKey in update.RemovedObjects)
            {
                if (m_PublicCollection.TryGetValue(removedKey, out T? removedObject))
                {
                    bool removed = m_PublicCollection.Remove(removedKey);
                    Debug.Assert(removed);

                    var objectData = m_InternalCollection[removedKey];
                    objectData.Object = default(T);
                    objectData.VersionNumber = newVersionNumber;

                    removedObjects.Add(removedObject);
                }
            }

            m_VersionNumber = newVersionNumber;

            // Now everything is applied, call the callbacks (so we are 100% sure they cannot mess up anything while
            // we update).
            foreach ((T obj, bool added) in updatedObjects)
            {
                if (added)
                {
                    ObjectAdded?.Invoke(obj);
                }
                else
                {
                    ObjectUpdated?.Invoke(obj);
                }
            }
            foreach (T obj in removedObjects)
            {
                ObjectRemoved?.Invoke(obj);
            }
            if (updatedObjects.Any() || removedObjects.Any())
            {
                SomethingChanged?.Invoke(this);
            }
        }

        /// <summary>
        /// Method designed to be used by unit tests to get (and validate) some internal data...
        /// </summary>
        /// <param name="key">Object's identifier</param>
        internal IncrementalCollectionObjectData<T> GetInternalObjectData(Guid key)
        {
            return m_InternalCollection[key];
        }

        #region ICollection<KeyValuePair> implementation
        void ICollection<KeyValuePair<Guid, T>>.Add(KeyValuePair<Guid, T> item)
        {
            Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<Guid, T>>.Contains(KeyValuePair<Guid, T> item)
        {
            if (m_PublicCollection.TryGetValue(item.Key, out T? value))
            {
                return item.Value.Equals(value);
            }
            return false;
        }

        void ICollection<KeyValuePair<Guid, T>>.CopyTo(KeyValuePair<Guid, T>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<Guid, T>>)m_PublicCollection).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<Guid, T>>.Remove(KeyValuePair<Guid, T> item)
        {
            if (((ICollection<KeyValuePair<Guid, T>>)this).Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }
        #endregion

        #region IEnumerable implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)m_PublicCollection).GetEnumerator();
        }
        #endregion

        #region IReadOnlyDictionary<Guid, T> implementation
        IEnumerable<Guid> IReadOnlyDictionary<Guid, T>.Keys => ((IReadOnlyDictionary<Guid, T>)m_PublicCollection).Keys;

        IEnumerable<T> IReadOnlyDictionary<Guid, T>.Values => ((IReadOnlyDictionary<Guid, T>)m_PublicCollection).Values;
        #endregion

        #region ICollection implementation
        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => throw new NotImplementedException();

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)m_PublicCollection).CopyTo(array, index);
        }
        #endregion

        #region IDictionary implementation
        bool IDictionary.IsFixedSize => false;

        ICollection IDictionary.Keys => m_PublicCollection.Keys;

        ICollection IDictionary.Values => m_PublicCollection.Values;

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)m_PublicCollection).GetEnumerator();
        }

        object? IDictionary.this[object key]
        {
            get => ((IDictionary)m_PublicCollection)[key];
            set
            {
                ValidateKeyParam(key);
                ValidateValueParam(value);
                this[(Guid)key] = (T)value!;
            }
        }

        void IDictionary.Add(object key, object? value)
        {
            ValidateKeyParam(key);
            ValidateValueParam(value);
            Add((Guid)key, (T)value!);
        }

        bool IDictionary.Contains(object key)
        {
            return (key is Guid guid) && ContainsKey(guid);
        }

        void IDictionary.Remove(object key)
        {
            if (key is Guid guid)
            {
                Remove(guid);
            }
        }
        #endregion

        /// <summary>
        /// Validate <paramref name="key"/> matches <paramref name="value"/>'s Id property.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <exception cref="ArgumentException">If <paramref name="key"/> != <paramref name="value"/>'s Id property.
        /// </exception>
        static void ValidateKeyMatchesValue(Guid key, T value)
        {
            if (key != value.Id)
            {
                throw new ArgumentException("Key and Value.Id does not match");
            }
        }

        /// <summary>
        /// Validate <paramref name="key"/> to be sure it is of the right type.
        /// </summary>
        /// <param name="key">Key</param>
        /// <exception cref="ArgumentException">If <paramref name="key"/> is not of the right type.</exception>
        static void ValidateKeyParam(object key)
        {
            if (key.GetType() != typeof(Guid))
            {
                throw new ArgumentException($"Key must be of type {typeof(Guid)}, was {key.GetType()}");
            }
        }

        /// <summary>
        /// Validate <paramref name="value"/> to be sure it is not null and is of the right type.
        /// </summary>
        /// <param name="value">Value</param>
        /// <exception cref="ArgumentException">If <paramref name="value"/> is not of the right type.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="value"/> is null.</exception>
        static void ValidateValueParam(object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value),"Value cannot be null.");
            }
            if (value.GetType() != typeof(T))
            {
                throw new ArgumentException($"Value must be of type {typeof(T)}, was {value.GetType()}");
            }
        }

        /// <summary>
        /// Main dictionary that contains all objects (even some historical information about deleted ones).
        /// </summary>
        Dictionary<Guid, IncrementalCollectionObjectData<T>> m_InternalCollection = new();
        /// <summary>
        /// Main dictionary that contains all objects really present in the collection from an external user point of
        /// view (no information about old deleted objects).
        /// </summary>
        Dictionary<Guid, T> m_PublicCollection = new();
        /// <summary>
        /// Collections version number (matches highest version number of all the objects in the collection).
        /// </summary>
        ulong m_VersionNumber;
    }
}
