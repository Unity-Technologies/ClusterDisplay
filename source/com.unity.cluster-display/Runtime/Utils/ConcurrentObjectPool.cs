using System;
using UnityEngine.Pool;

namespace Utils
{
    /// <summary>
    /// Wrapper around <see cref="ObjectPool{T}"/> to make it "thread safe" (methods can be called from multiple threads
    /// concurrently).
    /// </summary>
    /// <typeparam name="T">Type of objects in the pool.</typeparam>
    class ConcurrentObjectPool<T>: IObjectPool<T> where T : class
    {
        public ConcurrentObjectPool(Func<T> createFunc, Action<T> actionOnGet = null, Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            m_Internal = new(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck,
                defaultCapacity, maxSize);
        }

        public T Get()
        {
            lock (m_Internal)
            {
                return m_Internal.Get();
            }
        }

        public PooledObject<T> Get(out T v)
        {
            lock (m_Internal)
            {
                return m_Internal.Get(out v);
            }
        }

        public void Release(T element)
        {
            lock (m_Internal)
            {
                m_Internal.Release(element);
            }
        }

        public void Clear()
        {
            lock (m_Internal)
            {
                m_Internal.Clear();
            }
        }

        public int CountInactive
        {
            get
            {
                lock (m_Internal)
                {
                    return m_Internal.CountInactive;
                }
            }
        }

        ObjectPool<T> m_Internal;
    }
}
