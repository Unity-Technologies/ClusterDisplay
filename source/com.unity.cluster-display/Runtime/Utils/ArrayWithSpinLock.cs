using System;
using System.Threading;

namespace Utils
{
    /// <summary>
    /// Helper bundling a <see cref="Array"/> with a spin lock mechanism to synchronize access to an <see cref="Array"/>.
    /// </summary>
    /// <typeparam name="T">Object in the array</typeparam>
    /// <remarks>The main use case for this class is to synchronize access to an array where concurrent accesses are
    /// rare.  You might get better performances with <c>lock</c> or <see cref="Monitor"/> if concurrent accesses are
    /// frequent enough or long enough.</remarks>
    class ArrayWithSpinLock<T>
    {
        /// <summary>
        /// Struct keeping the <see cref="ArrayWithSpinLock{T}"/> locked until disposed of.
        /// </summary>
        /// <remarks>Classic use case is:
        /// <code>
        /// var concurrentArray = new ArrayWithSpinLock&lt;int&gt;();
        /// using (var arrayLock = concurrentArray.lock())
        /// {
        ///     // Get the array using arrayLock.GetArray()
        ///     // Set the array using arrayLock.SetArray()
        /// }
        /// </code>
        /// </remarks>
        public struct LockedArray: IDisposable
        {
            internal LockedArray(ArrayWithSpinLock<T> theArray)
            {
                m_TheArray = theArray;
            }

            public void Dispose()
            {
                m_TheArray.Unlock();
            }

            /// <summary>
            /// Returns the <see cref="Array"/> contained in the <see cref="ArrayWithSpinLock{T}"/>.
            /// </summary>
            public T[] GetArray() => m_TheArray.m_Array;

            /// <summary>
            /// Sets the <see cref="Array"/> contained in the <see cref="ArrayWithSpinLock{T}"/>.
            /// </summary>
            /// <remarks>Cannot use a property with a Get / Set because it would cause error when used with a using
            /// keyword.</remarks>
            public void SetArray(T[] value) => m_TheArray.m_Array = value;

            /// <summary>
            /// Append an item at the end of the array.
            /// </summary>
            /// <param name="item">Item to append.</param>
            /// <remarks>A new array is reallocated every time this method is called, so it shouldn't be called to often.
            /// </remarks>
            public void AppendToArray(T item)
            {
                var newArray = new T[GetArray().Length + 1];
                GetArray().CopyTo(newArray, 0);
                newArray[^1] = item;
                SetArray(newArray);
            }

            /// <summary>
            /// Remove an item from the array.
            /// </summary>
            /// <param name="item">To remove from the array.</param>
            /// <remarks>A new array is reallocated every time this method is called, so it shouldn't be called to often.
            /// </remarks>
            public void RemoveFromArray(T item)
            {
                int index = Array.IndexOf(GetArray(), item);
                if (index >= 0)
                {
                    var newArray = new T[GetArray().Length - 1];
                    if (index > 0)
                    {
                        Array.Copy(GetArray(), 0, newArray, 0, index);
                    }
                    if (index + 1 < GetArray().Length)
                    {
                        Array.Copy(GetArray(), index + 1, newArray, index, newArray.Length - index);
                    }
                    SetArray(newArray);
                }
            }

            ArrayWithSpinLock<T> m_TheArray;
        }

        /// <summary>
        /// Lock the array so that other thread have to wait that <see cref="Unlock"/> is called to access it.
        /// </summary>
        public LockedArray Lock()
        {
            while (Interlocked.CompareExchange(ref m_IsInUse, 1, 0) != 0);
            return new LockedArray(this);
        }

        /// <summary>
        /// Unlock the array allowing other threads to access it.
        /// </summary>
        public void Unlock()
        {
            m_IsInUse = 0;
        }

        int m_IsInUse;
        T[] m_Array = new T[0];
    }
}
