using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// Maintains a collection of objects and tracks references to them. Automatically
    /// allocates/disposes based on the current number of active shared references.
    /// </summary>
    /// <typeparam name="TKey">Type used to initialize and identify a shared object.</typeparam>
    /// <typeparam name="TClass">Type of the shared objects.</typeparam>
    /// <remarks>
    /// You can use this class to manage a resource shared by multiple objects that you want to automatically dispose
    /// when no longer in use. For example, a network socket (the class) bound to a specific port (the key).
    /// Not thread-safe.
    /// </remarks>
    class SharedReferenceManager<TKey, TClass>
        where TKey : IEquatable<TKey>
        where TClass : class, IDisposable
    {
        Func<TKey, TClass> m_CreateFunc;

        class CountedReference
        {
            public TClass Value { get; }
            public int Count { get; private set; }

            public void Increment() => ++Count;
            public void Decrement() => --Count;

            public CountedReference(TClass value)
            {
                Value = value;
                Count = 1;
            }
        }

        readonly Dictionary<TKey, CountedReference> m_References = new();

        /// <summary>
        /// A shared reference wrapping a <typeparamref name="SharedReferenceManager.TClass"/>.
        /// Calling <see cref="Dispose"/> releases the reference.
        /// </summary>
        public class SharedRef : IDisposable
        {
            TKey m_Key;
            SharedReferenceManager<TKey, TClass> m_Ref;

            internal SharedRef(TKey key, SharedReferenceManager<TKey, TClass> autoReleaseRef)
            {
                m_Key = key;
                m_Ref = autoReleaseRef;
                Value = m_Ref.Reserve(key);
            }

            public void Dispose()
            {
                m_Ref?.Release(m_Key);
                Value = null;
            }

            public TClass Value { get; private set; }

            public static implicit operator TClass(SharedRef sharedRef) => sharedRef?.Value;
        }

        /// <summary>
        /// Creates a new <see cref="SharedReferenceManager{TKey,TClass}"/>
        /// </summary>
        /// <param name="createFunc">
        /// A delegate that takes an argument of type <typeparamref name="TKey"/> and returns
        /// a new <typeparamref name="TClass"/>.
        /// </param>
        public SharedReferenceManager(Func<TKey, TClass> createFunc)
        {
            m_CreateFunc = createFunc;
        }

        /// <summary>
        /// Get a shared reference. If there are no current users, it will create a new instance
        /// using the supplied delegate.
        /// </summary>
        /// <param name="arg">A unique identifier; also an initialization argument.</param>
        /// <returns></returns>
        public SharedRef GetReference(TKey arg)
        {
            return new SharedRef(arg, this);
        }

        TClass Reserve(TKey key)
        {
            if (m_References.TryGetValue(key, out var reference))
            {
                reference.Increment();
                return reference.Value;
            }

            var instance = m_CreateFunc(key);
            m_References.Add(key, new CountedReference(instance));

            return instance;
        }

        void Release(TKey key)
        {
            if (!m_References.TryGetValue(key, out var reference)) return;

            reference.Decrement();

            if (reference.Count == 0)
            {
                reference.Value.Dispose();
                m_References.Remove(key);
            }
        }
    }
}
