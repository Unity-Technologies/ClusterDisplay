using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// This class provides a strongly-typed API for propagating events across the cluster
    /// (from Emitter to Repeaters).
    /// </summary>
    /// <remarks>
    /// EventBus wraps <see cref="EmitterStateWriter.RegisterOnStoreCustomDataDelegate"/> and
    /// <see cref="RepeaterStateReader.RegisterOnLoadDataDelegate"/>.
    /// </remarks>
    /// <typeparam name="TEvent">Type of the events managed by this bus.</typeparam>
    class EventBus<TEvent> : IDisposable where TEvent : unmanaged
    {
        /// <summary>
        /// Delegate for receiving a contiguous array of event objects.
        /// </summary>
        public delegate void BulkEventListener(ReadOnlySpan<TEvent> eventData);

        const int k_MaxBuffer = 128;
        int m_OutBufferLength;

        NativeArray<TEvent> m_OutBuffer = new(k_MaxBuffer, Allocator.Persistent);
        readonly List<Action<TEvent>> m_Listeners = new();
        readonly List<BulkEventListener> m_BulkListeners = new();

        static readonly int k_FrameDataId = (int)StateID.CustomData ^ typeof(TEvent).DeterministicTypeID();

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
        /// Enqueues an event on the bus. The event is propagated during the next sync point.
        /// </summary>
        /// <param name="eventData">The event data to be published.</param>
        /// <returns><see langword="false"/> if the queue is full. <see langword="true"/> if successfully queued.</returns>
        public bool Publish(TEvent eventData)
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
        /// <param name="listener">The event listener.</param>
        /// <returns>An object that can be used to unsubscribe from the bus when disposed.</returns>
        public IDisposable Subscribe(Action<TEvent> listener)
        {
            m_Listeners.Add(listener);
            return new Unsubscriber<Action<TEvent>>(m_Listeners, listener);
        }

        /// <summary>
        /// Listen for an "array" of events. This is efficient if the listener processes the event data in bulk.
        /// </summary>
        /// <param name="bulkListener"></param>
        /// <returns>An object that can be used to unsubscribe from the bus when disposed.</returns>
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
        /// <param name="rawData">Raw data representing a serialized sequence of events of type <typeparamref name="TEvent"/></param>
        /// <returns><see langword="true"/> if deserialized correctly and listeners did not throw.</returns>
        public bool DeserializeAndPublish(ReadOnlySpan<byte> rawData)
        {
            try
            {
                var data = MemoryMarshal.Cast<byte, TEvent>(rawData);
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
            catch (Exception e)
            {
                ClusterDebug.LogError(e.Message);
                return false;
            }
        }

        /// <summary>
        /// This is an overload provided to be compatible with the cluster data API.
        /// </summary>
        /// <param name="rawData">Raw data representing a serialized sequence of events of type <typeparamref name="TEvent"/></param>
        /// <returns><see langword="true"/> if deserialized correctly and listeners did not throw.</returns>
        public bool DeserializeAndPublish(NativeArray<byte> rawData) => DeserializeAndPublish(rawData.AsReadOnlySpan());

        /// <summary>
        /// Serialize enqueued events into a byte buffer and clears the queue.
        /// </summary>
        /// <param name="outBuffer">The buffer to hold the serialized events.</param>
        /// <returns>The number of bytes copied to <paramref name="outBuffer"/>></returns>
        int SerializeAndFlush(NativeArray<byte> outBuffer)
        {
            var bytes = MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsSpan());
            var bytesWritten = bytes.TryCopyTo(outBuffer.AsSpan()) ? bytes.Length : 0;
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

    static class EventHelperExtensions
    {
        /// <summary>
        /// An implementation of the djb2 algorithm
        /// http://www.cse.yorku.ca/~oz/hash.html
        /// </summary>
        public static int DeterministicTypeID(this Type type)
        {
            var name = type.AssemblyQualifiedName;
            Debug.Assert(name != null, nameof(name) + " != null");

            var hash = 5381UL;
            foreach (var c in name)
            {
                hash = ((hash << 5) + hash) + c;
            }

            return (int)hash;
        }
    }
}
