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
    /// <remarks>Could be replaced with System.Threading.Channels, but it is not yet part of .Net Standard 2.1.</remarks>
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
        /// Removes and returns the object at the beginning of the <see cref="BlockingQueue{T}"/>.
        /// </summary>
        /// <returns>Object in front of the queue.</returns>
        /// <remarks>The call will block if no object are present in the queue.</remarks>
        public T Dequeue()
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
        /// Removes the object at the beginning of the <see cref="BlockingQueue{T}"/>, and copies it to the result
        /// parameter.
        /// </summary>
        /// <param name="result">The removed object.</param>
        /// <returns><c>true</c> if the object is successfully removed; <c>false</c> if the <see cref="BlockingQueue{T}"/>
        /// is empty.</returns>
        public bool TryDequeue(out T result)
        {
            lock (m_Internal)
            {
                return m_Internal.TryDequeue(out result);
            }
        }

        /// <summary>
        /// Removes the object at the beginning of the <see cref="BlockingQueue{T}"/> (waiting for up to
        /// <paramref name="timeout"/> if empty), and copies it to the result parameter.
        /// </summary>
        /// <param name="result">The removed object.</param>
        /// <param name="timeout">Maximum amount of time to wait for an object.</param>
        /// <returns><c>true</c> if the object is successfully removed; <c>false</c> if the <see cref="BlockingQueue{T}"/>
        /// is empty for <paramref name="timeout"/>.</returns>
        public bool TryDequeue(out T result, TimeSpan timeout)
        {
            long deadlineTimestamp = StopwatchUtils.TimestampIn(timeout);
            lock (m_Internal)
            {
                do
                {
                    if (m_Internal.TryDequeue(out result))
                    {
                        return true;
                    }

                    Monitor.Wait(m_Internal, StopwatchUtils.TimeUntil(deadlineTimestamp));
                } while (Stopwatch.GetTimestamp() < deadlineTimestamp);

                return false;
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
