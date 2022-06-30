using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.ClusterDisplay;

namespace Utils
{
    /// <summary>
    /// A queue that has method to block when trying to dequeue and there is no content.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    /// <remarks>Based on a normal <see cref="Queue{T}"/> instead of a
    /// <see><cref>BlockingCollection{T}.TryTake(T, Timespan)</cref></see> since some tests revealed that it would
    /// result in 7 heap allocations per dequeue calls.</remarks>
    /// <remarks>We are not systematically exposing methods or interfaces of <see cref="Queue{T}"/> as they are not
    /// necessarily concurrency friendly.</remarks>
    class BlockingQueue<T> where T: class
    {
        public BlockingQueue()
        {
            m_Internal = new();
        }

        public BlockingQueue(int capacity)
        {
            m_Internal = new(capacity);
        }

        public BlockingQueue(IEnumerable<T> collection)
        {
            m_Internal = new(collection);
        }

        /// <summary>
        /// Consumes (returns and removes from the queue) the object in front of the queue.
        /// </summary>
        /// <returns>Object in front of the queue.</returns>
        /// <remarks>The call will block if no object are present in the queue.</remarks>
        public T ConsumeNext()
        {
            lock (m_Internal)
            {
                for (;;)
                {
                    if (m_Internal.TryDequeue(out var ret))
                    {
                        return ret;
                    }

                    Monitor.Wait(m_Internal);
                }
            }
        }

        /// <summary>
        /// Tries to consume (return and remove from the queue) the object in front of the queue.
        /// </summary>
        /// <returns>Object in front of the queue.</returns>
        public T TryConsumeNext()
        {
            lock (m_Internal)
            {
                m_Internal.TryDequeue(out var ret);
                return ret;
            }
        }

        /// <summary>
        /// Tries to consume (return and remove from the queue) the object in front of the queue and wait the specified
        /// time period if the queue is empty.
        /// </summary>
        /// <param name="timeout">Maximum amount of time to wait for an object.</param>
        /// <returns>Object in front of the queue or <c>null</c> if waited for at least <paramref name="timeout"/> and
        /// no object was received.</returns>
        public T TryConsumeNext(TimeSpan timeout)
        {
            long deadlineTimestamp = StopwatchUtils.TimestampIn(timeout);
            lock (m_Internal)
            {
                do
                {
                    if (m_Internal.TryDequeue(out var ret))
                    {
                        return ret;
                    }

                    Monitor.Wait(m_Internal, StopwatchUtils.TimeUntil(deadlineTimestamp));
                } while (Stopwatch.GetTimestamp() < deadlineTimestamp);

                return null;
            }
        }

        /// <summary>
        /// Number of objects currently in the queue.
        /// </summary>
        /// <remarks>Caller should keep in mind that by the time it receives the returned value the actual count might
        /// have changed.</remarks>
        public int Count
        {
            get
            {
                lock (m_Internal)
                {
                    return m_Internal.Count;
                }
            }
        }

        /// <summary>
        /// Adds an element to the back of the queue.
        /// </summary>
        /// <param name="item">To add to the queue.</param>
        public void Enqueue(T item)
        {
            lock (m_Internal)
            {
                m_Internal.Enqueue(item);
                Monitor.Pulse(m_Internal);
            }
        }

        Queue<T> m_Internal;
    }
}
