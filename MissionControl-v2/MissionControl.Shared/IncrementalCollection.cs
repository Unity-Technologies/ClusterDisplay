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
        , IIncrementalCollectionObjectChangeObserver
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
        /// Event called to inform that something in the collection changed (merge of <see cref="OnObjectAdded"/>,
        /// <see cref="OnObjectRemoved"/> and <see cref="OnObjectUpdated"/> without any generic parameter).
        /// </summary>
        public event Action<IReadOnlyIncrementalCollection>? OnSomethingChanged;

        /// <summary>
        /// Add a <typeparamref name="T"/> to the collection (similar to
        /// <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/> but with one less parameter).
        /// </summary>
        /// <param name="obj">The <typeparamref name="T"/> to add to the collection.</param>
        public void Add(T obj)
        {
            ulong newVersionNumber = m_VersionNumber + 1;
            ulong newFirstVersionNumber = newVersionNumber;

            m_TDictionary.Add(obj.Id, obj);
            if (m_Dictionary.TryGetValue(obj.Id, out var previousValue))
            {
                Debug.Assert(previousValue is IncrementalCollectionRemovedMarker);
                Debug.Assert(previousValue.ChangeObserver == this);
                previousValue.ChangeObserver = null;
                newFirstVersionNumber = previousValue.FirstVersionNumber;
            }
            m_Dictionary[obj.Id] = obj;
            obj.FirstVersionNumber = newFirstVersionNumber;
            obj.VersionNumber = newVersionNumber;
            obj.ChangeObserver = this;
            m_VersionNumber = newVersionNumber;

            OnObjectAdded?.Invoke(obj);
            OnSomethingChanged?.Invoke(this);
        }

        public T this[Guid key]
        {
            get => m_TDictionary[key];
            set
            {
                ValidateKeyMatchesValue(key, value);
                ulong newVersionNumber = m_VersionNumber + 1;
                ulong newFirstVersionNumber = newVersionNumber;
                bool added = true;
                if (m_Dictionary.TryGetValue(key, out var previousValue))
                {
                    newFirstVersionNumber = previousValue.FirstVersionNumber;
                    Debug.Assert(previousValue.ChangeObserver == this);
                    previousValue.ChangeObserver = null;
                    added = previousValue is IncrementalCollectionRemovedMarker;
                }
                value.FirstVersionNumber = newFirstVersionNumber;
                value.VersionNumber = newVersionNumber;
                value.ChangeObserver = this;
                m_Dictionary[key] = value;
                m_TDictionary[key] = value;
                m_VersionNumber = newVersionNumber;

                if (added)
                {
                    OnObjectAdded?.Invoke(value);
                }
                else
                {
                    OnObjectUpdated?.Invoke(value);
                }
                OnSomethingChanged?.Invoke(this);
            }
        }

        public ICollection<Guid> Keys => m_TDictionary.Keys;

        public ICollection<T> Values => m_TDictionary.Values;

        public int Count => m_TDictionary.Count;

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
            foreach (var pair in m_TDictionary)
            {
                Debug.Assert(pair.Value.ChangeObserver == this);
                pair.Value.ChangeObserver = null;
                IncrementalCollectionRemovedMarker removedMarker = new(pair.Key);
                removedMarker.FirstVersionNumber = pair.Value.FirstVersionNumber;
                removedMarker.VersionNumber = newVersionNumber;
                removedMarker.ChangeObserver = this;
                m_Dictionary[pair.Key] = removedMarker;
            }

            var oldDictionary = m_TDictionary;
            m_TDictionary = new();

            m_VersionNumber = newVersionNumber;

            foreach (var removedObject in oldDictionary.Values)
            {
                OnObjectRemoved?.Invoke(removedObject);
            }
            OnSomethingChanged?.Invoke(this);
        }

        public bool ContainsKey(Guid key)
        {
            return m_TDictionary.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<Guid, T>> GetEnumerator()
        {
            return m_TDictionary.GetEnumerator();
        }

        public bool Remove(Guid key)
        {
            if (m_TDictionary.TryGetValue(key, out T? removedObject))
            {
                Debug.Assert(removedObject.ChangeObserver == this);
                removedObject.ChangeObserver = null;

                bool removed = m_TDictionary.Remove(key);
                Debug.Assert(removed);

                ulong newVersionNumber = m_VersionNumber + 1;
                IncrementalCollectionRemovedMarker removedMarker = new(key);
                removedMarker.FirstVersionNumber = removedObject.FirstVersionNumber;
                removedMarker.VersionNumber = newVersionNumber;
                removedMarker.ChangeObserver = this;
                m_Dictionary[key] = removedMarker;
                m_VersionNumber = newVersionNumber;

                OnObjectRemoved?.Invoke(removedObject);
                OnSomethingChanged?.Invoke(this);

                return true;
            }
            return false;
        }

        public bool TryGetValue(Guid key, [MaybeNullWhen(false)] out T value)
        {
            return m_TDictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Version number of the collection (in increments every time something changes in the collection).
        /// </summary>
        public ulong VersionNumber => m_VersionNumber;

        /// <summary>
        /// Compute what changed since the reference version number.
        /// </summary>
        /// <param name="sinceVersionNumber">Returns all the changes for which VersionNumber >= sinceVersionNumber</param>
        public IncrementalCollectionUpdate<T> GetDeltaSince(UInt64 sinceVersionNumber)
        {
            var updatedObjects = new List<T>();
            var removedObjects = new List<Guid>();
            foreach (IncrementalCollectionObject incrementalObject in m_Dictionary.Values)
            {
                if (incrementalObject.VersionNumber >= sinceVersionNumber)
                {
                    if (incrementalObject is T updatedObject)
                    {
                        T clone = updatedObject.DeepClone();
                        updatedObjects.Add(clone);
                    }
                    else
                    {
                        Debug.Assert(incrementalObject is IncrementalCollectionRemovedMarker);
                        var removedMarker = (IncrementalCollectionRemovedMarker)incrementalObject;
                        if (removedMarker.FirstVersionNumber < sinceVersionNumber)
                        {
                            removedObjects.Add(incrementalObject.Id);
                        }
                    }
                }
            }

            var ret = new IncrementalCollectionUpdate<T>();
            ret.UpdatedObjects = updatedObjects.AsEnumerable();
            ret.RemovedObjects = removedObjects.AsEnumerable();
            ret.NextUpdate = VersionNumber + 1;
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
                if (m_Dictionary.TryGetValue(updatedObject.Id, out var previousObject) && previousObject is T previousT)
                {
                    previousT.DeepCopy(updatedObject);
                    previousT.VersionNumber = newVersionNumber;
                    updatedObjects.Add((previousT, false));
                }
                else
                {
                    if (previousObject != null)
                    {
                        Debug.Assert(previousObject is IncrementalCollectionRemovedMarker);
                        Debug.Assert(previousObject.ChangeObserver == this);
                        previousObject.ChangeObserver = null;
                    }

                    T newObject = updatedObject.DeepClone();
                    newObject.FirstVersionNumber = newVersionNumber;
                    newObject.VersionNumber = newVersionNumber;
                    newObject.ChangeObserver = this;
                    m_Dictionary[updatedObject.Id] = newObject;
                    m_TDictionary[updatedObject.Id] = newObject;

                    updatedObjects.Add((newObject, true));
                }
            }

            List<T> removedObjects = new (update.RemovedObjects.Count());
            foreach (Guid removedKey in update.RemovedObjects)
            {
                if (m_TDictionary.TryGetValue(removedKey, out T? removedObject))
                {
                    Debug.Assert(removedObject.ChangeObserver == this);
                    removedObject.ChangeObserver = null;

                    bool removed = m_TDictionary.Remove(removedKey);
                    Debug.Assert(removed);

                    IncrementalCollectionRemovedMarker removedMarker = new(removedKey);
                    removedMarker.FirstVersionNumber = removedObject.FirstVersionNumber;
                    removedMarker.VersionNumber = newVersionNumber;
                    removedMarker.ChangeObserver = this;
                    m_Dictionary[removedKey] = removedMarker;

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
                    OnObjectAdded?.Invoke(obj);
                }
                else
                {
                    OnObjectUpdated?.Invoke(obj);
                }
            }
            foreach (T obj in removedObjects)
            {
                OnObjectRemoved?.Invoke(obj);
            }
            if (updatedObjects.Any() || removedObjects.Any())
            {
                OnSomethingChanged?.Invoke(this);
            }
        }

        #region ICollection<KeyValuePair> implementation
        void ICollection<KeyValuePair<Guid, T>>.Add(KeyValuePair<Guid, T> item)
        {
            Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<Guid, T>>.Contains(KeyValuePair<Guid, T> item)
        {
            if (m_TDictionary.TryGetValue(item.Key, out T? value))
            {
                return item.Value.Equals(value);
            }
            return false;
        }

        void ICollection<KeyValuePair<Guid, T>>.CopyTo(KeyValuePair<Guid, T>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<Guid, T>>)m_TDictionary).CopyTo(array, arrayIndex);
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
            return ((IEnumerable)m_TDictionary).GetEnumerator();
        }
        #endregion

        #region IReadOnlyDictionary<Guid, T> implementation
        IEnumerable<Guid> IReadOnlyDictionary<Guid, T>.Keys => ((IReadOnlyDictionary<Guid, T>)m_TDictionary).Keys;

        IEnumerable<T> IReadOnlyDictionary<Guid, T>.Values => ((IReadOnlyDictionary<Guid, T>)m_TDictionary).Values;
        #endregion

        #region ICollection implementation
        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => throw new NotImplementedException();

        void ICollection.CopyTo(Array array, int index)
        {
            ((ICollection)m_TDictionary).CopyTo(array, index);
        }
        #endregion

        #region IDictionary implementation
        bool IDictionary.IsFixedSize => false;

        ICollection IDictionary.Keys => m_TDictionary.Keys;

        ICollection IDictionary.Values => m_TDictionary.Values;

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return ((IDictionary)m_TDictionary).GetEnumerator();
        }

        object? IDictionary.this[object key]
        {
            get => ((IDictionary)m_TDictionary)[key];
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

        #region IIncrementalCollectionObjectChangeObserver implementation
        void IIncrementalCollectionObjectChangeObserver.ObjectChanged(IncrementalCollectionObject obj)
        {
            ++m_VersionNumber;
            obj.VersionNumber = m_VersionNumber;

            // IncrementalCollectionRemovedMarker should never change, so this delegate should never be called so obj
            // should always be a T.
            Debug.Assert(obj is T);
            OnObjectUpdated?.Invoke((T)obj);
            OnSomethingChanged?.Invoke(this);
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
        /// Main dictionary that contains all objects (and <see cref="IncrementalCollectionRemovedMarker"/> for
        /// removed objects).
        /// </summary>
        Dictionary<Guid, IncrementalCollectionObject> m_Dictionary = new();
        /// <summary>
        /// Main dictionary that contains all objects really present in the collection (no
        /// <see cref="IncrementalCollectionRemovedMarker"/>).
        /// </summary>
        Dictionary<Guid, T> m_TDictionary = new();
        /// <summary>
        /// Collections version number (matches highest version number of all the objects in the collection).
        /// </summary>
        ulong m_VersionNumber;
    }
}
