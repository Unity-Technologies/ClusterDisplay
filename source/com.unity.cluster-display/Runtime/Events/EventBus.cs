using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// This class provides a strongly-typed API for propagating data across the cluster
    /// (from Emitter to Repeaters).
    /// </summary>
    /// <remarks>
    /// EventBus wraps <see cref="EmitterStateWriter.RegisterOnStoreCustomDataDelegate"/> and
    /// <see cref="RepeaterStateReader.RegisterOnLoadDataDelegate"/>.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    class EventBus<T> : IDisposable where T : unmanaged
    {
        /// <summary>
        /// Delegate for receiving a contiguous array of objects.
        /// </summary>
        public delegate void BulkEventListener(ReadOnlySpan<T> eventData);

        const int k_MaxBuffer = 128;
        int m_OutBufferLength;

        NativeArray<T> m_OutBuffer = new(k_MaxBuffer, Allocator.Persistent);
        readonly List<Action<T>> m_Listeners = new();
        readonly List<BulkEventListener> m_BulkListeners = new();

        static readonly int k_FrameDataId = (typeof(T).GetHashCode() * 397) ^ (int) StateID.CustomData;

        internal ReadOnlySpan<byte> OutBuffer =>
            MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsReadOnlySpan());

        public EventBus()
        {
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate(k_FrameDataId, SerializeAndFlush);
            RepeaterStateReader.RegisterOnLoadDataDelegate(k_FrameDataId, DeserializeAndPublish);
        }

        class Unsubscriber<TListener> : IDisposable
        {
            readonly List<TListener> m_ListenerList;
            readonly TListener m_Listener;

            public Unsubscriber([NotNull] List<TListener> listenerList, [NotNull] TListener listener)
            {
                m_ListenerList = listenerList;
                m_Listener = listener;
            }

            public void Dispose()
            {
                m_ListenerList.Remove(m_Listener);
            }
        }

        /// <summary>
        /// Enqueues data on the bus. The data is propagated during the next sync point.
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        public bool Publish(T eventData)
        {
            if (m_OutBufferLength < m_OutBuffer.Length - 1)
            {
                m_OutBuffer[m_OutBufferLength++] = eventData;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Listen for events.
        /// </summary>
        /// <param name="listener"></param>
        /// <returns>An object that can be used to unsubscribe from the bus when disposed.</returns>
        public IDisposable Subscribe(Action<T> listener)
        {
            m_Listeners.Add(listener);
            return new Unsubscriber<Action<T>>(m_Listeners, listener);
        }

        /// <summary>
        /// Listen for an "array" of events. This is efficient if the listener processes the data in bulk.
        /// </summary>
        /// <param name="bulkListener"></param>
        /// <returns></returns>
        public IDisposable Subscribe(BulkEventListener bulkListener)
        {
            m_BulkListeners.Add(bulkListener);
            return new Unsubscriber<BulkEventListener>(m_BulkListeners, bulkListener);
        }

        /// <summary>
        /// Convert a byte buffer to events and invoke the listeners.
        /// </summary>
        /// <remarks>
        /// You will not typically call this directly. This method is registered with the low-level
        /// cluster data API and will be invoked automatically when running in a cluster.
        /// </remarks>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public bool DeserializeAndPublish(ReadOnlySpan<byte> rawData)
        {
            var data = MemoryMarshal.Cast<byte, T>(rawData);
            foreach (var item in data)
            {
                foreach (var listener in m_Listeners)
                {
                    listener.Invoke(item);
                }
            }

            foreach (var bulkListener in m_BulkListeners)
            {
                bulkListener.Invoke(data);
            }

            return true;
        }

        /// <summary>
        /// This is an overload provided to be compatible with the cluster data API.
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public bool DeserializeAndPublish(NativeArray<byte> rawData) => DeserializeAndPublish(rawData.AsReadOnlySpan());

        /// <summary>
        /// Serialize enqueued events into a byte buffer and clears the queue.
        /// </summary>
        /// <param name="outData"></param>
        /// <returns></returns>
        int SerializeAndFlush(NativeArray<byte> outData)
        {
            var bytes = MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsSpan());
            var bytesWritten = bytes.TryCopyTo(outData.AsSpan()) ? bytes.Length : 0;
            m_OutBufferLength = 0;
            return bytesWritten;
        }

        /// <summary>
        /// Unregister with the cluster data API.
        /// </summary>
        public void Dispose()
        {
            EmitterStateWriter.UnregisterCustomDataDelegate(k_FrameDataId);
            RepeaterStateReader.UnregisterOnLoadDataDelegate(k_FrameDataId);
        }
    }
}
