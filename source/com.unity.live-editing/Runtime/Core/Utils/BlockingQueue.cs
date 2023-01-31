using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Unity.LiveEditing.LowLevel
{
    /// <summary>
    /// A simple thread-safe blocking queue that favors minimizing GC allocations
    /// over contentions.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    class BlockingQueue<T>
    {
        readonly Queue<T> m_Queue;
        readonly object m_Lock = new();
        bool m_Completed;

        /// <summary>
        /// Initializes a new instance with an initial capacity.
        /// </summary>
        /// <remarks>
        /// The capacity may grow as items are added.
        /// </remarks>
        /// <param name="initialCapacity">The initial capacity.</param>
        public BlockingQueue(int initialCapacity)
        {
            m_Queue = new Queue<T>(initialCapacity);
        }

        public int Count
        {
            get
            {
                int count;
                lock (m_Lock)
                {
                    count = m_Queue.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// Tries to add the item to the collection.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <returns><c>true</c> if the item was added successfully, otherwise <c>false</c> if the collection
        /// was marked as "complete" with <see cref="CompleteAdding"/> (not accepting any more additions).</returns>
        public bool TryEnqueue(T item)
        {
            lock (m_Lock)
            {
                if (m_Completed)
                {
                    return false;
                }
                m_Queue.Enqueue(item);
                Monitor.PulseAll(m_Lock);
            }

            return true;
        }

        /// <summary>
        /// Removes an and item from the queue.
        /// </summary>
        /// <returns>The item that was removed from the queue.</returns>
        /// <remarks>A call may block until an item is available to be removed.</remarks>
        public T Dequeue()
        {
            T item = default;
            lock (m_Lock)
            {
                for (;;)
                {
                    if (m_Queue.Count > 0)
                    {
                        item = m_Queue.Dequeue();
                        break;
                    }
                    if (m_Completed)
                    {
                        break;
                    }
                    Monitor.Wait(m_Lock);
                }
            }

            return item;
        }

        /// <summary>
        /// Tries to remove an item from the queue.
        /// </summary>
        /// <param name="item">The item removed from the collection.</param>
        /// <returns><c>true</c> if an item could be removed; otherwise <c>false</c></returns>
        public bool TryDequeue(out T item)
        {
            item = default;
            lock (m_Lock)
            {
                if (m_Queue.Count > 0)
                {
                    item = m_Queue.Dequeue();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mark the queue as no longer accepting any more additions.
        /// </summary>
        /// <remarks>
        /// After a queue as been marked as complete, adding to the queue is no longer permitted, and all
        /// calls to <see cref="Dequeue"/> will immediately return the default element (block calls will immediately
        /// unblock).
        /// </remarks>
        public void CompleteAdding()
        {
            lock (m_Lock)
            {
                m_Completed = true;
                Monitor.PulseAll(m_Lock);
            }
        }
    }
}
