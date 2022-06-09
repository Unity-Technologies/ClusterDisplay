using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    public class AutoReleaseReference<TKey, TClass>
        where TKey : IEquatable<TKey>
        where TClass : class, IDisposable
    {
        int m_RefCount;
        Func<TKey, TClass> m_CreateFunc;

        struct CountedReference
        {
            public TClass Value;
            public int Count;
        }

        readonly Dictionary<TKey, CountedReference> m_References = new();

        public class SharedRef : IDisposable
        {
            TKey m_Key;
            AutoReleaseReference<TKey, TClass> m_Ref;
            TClass m_Value;

            public SharedRef(TKey key, AutoReleaseReference<TKey, TClass> autoReleaseRef)
            {
                m_Key = key;
                m_Ref = autoReleaseRef;
                m_Value = m_Ref.Reserve(key);
            }

            public void Dispose()
            {
                m_Ref.Release(m_Key);
            }

            public static implicit operator TClass(SharedRef @ref) => @ref?.m_Value;
        }

        public AutoReleaseReference(Func<TKey, TClass> createFunc)
        {
            m_CreateFunc = createFunc;
        }

        public SharedRef GetReference(TKey arg)
        {
            return new SharedRef(arg, this);
        }

        TClass Reserve(TKey key)
        {
            if (m_References.TryGetValue(key, out var reference))
            {
                reference.Count++;
                m_References[key] = reference;
                return reference.Value;
            }

            var instance = m_CreateFunc(key);
            m_References.Add(key, new CountedReference
            {
                Value = instance,
                Count = 1
            });

            return instance;
        }

        void Release(TKey key)
        {
            if (!m_References.TryGetValue(key, out var reference)) return;

            reference.Count--;

            if (reference.Count == 0)
            {
                reference.Value.Dispose();
                m_References.Remove(key);
            }
            else
            {
                m_References[key] = reference;
            }
        }
    }
}
