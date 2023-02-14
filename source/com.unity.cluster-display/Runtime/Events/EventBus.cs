using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Unity.ClusterDisplay.Utils;
using Unity.Collections;
using UnityEngine;
using IdType = System.Guid;

namespace Unity.ClusterDisplay
{
    [Flags]
    public enum EventBusFlags
    {
        None = 0,
        ReadFromCluster = 1,
        WriteToCluster = 2,
        Loopback = 4
    }

    /// <summary>
    /// This class provides a strongly-typed API for propagating events across the cluster
    /// (from Emitter to Repeaters).
    /// </summary>
    /// <remarks>
    /// EventBus wraps <see cref="EmitterStateWriter.RegisterOnStoreCustomDataDelegate"/> and
    /// <see cref="RepeaterStateReader.RegisterOnLoadDataDelegate"/>.
    /// </remarks>
    /// <typeparam name="TEvent">Type of the events managed by this bus.</typeparam>
    public class EventBus<TEvent> : IDisposable where TEvent : unmanaged
    {
        /// <summary>
        /// Delegate for receiving a contiguous array of event objects.
        /// </summary>
        public delegate void BulkEventListener(ReadOnlySpan<TEvent> eventData);

        const int k_MaxBuffer = 128;
        int m_OutBufferLength;
        int m_InBufferLength;

        NativeArray<TEvent> m_OutBuffer = new(k_MaxBuffer, Allocator.Persistent);
        NativeArray<TEvent> m_LoopbackBuffer = new(k_MaxBuffer, Allocator.Persistent);

        readonly List<Action<TEvent>> m_Listeners = new();
        readonly List<BulkEventListener> m_BulkListeners = new();
        readonly bool m_IsReadOnly;

        // ReSharper disable once StaticMemberInGenericType
        static readonly Guid k_NamespaceId = Guid.Parse("8876618a-f18a-11ec-8ea0-0242ac120002");

        // Create a (effectively) unique identifier for this event bus based on the full-qualified name
        // of the event type.
        static IdType s_EventTypeId = GuidUtils.GetNameBasedGuid(k_NamespaceId,
            typeof(TEvent).GetFriendlyTypeName());

        readonly int k_DataId;

        internal ReadOnlySpan<byte> OutBuffer =>
            MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsReadOnlySpan());

        public EventBus(int id = (int)StateID.CustomEvents)
            : this(EventBusFlags.None, id) { }

        public EventBus([NotNull] IClusterSyncState clusterSyncState, int id = (int)StateID.CustomEvents)
            : this(clusterSyncState.NodeRole switch
            {
                NodeRole.Emitter => EventBusFlags.Loopback | EventBusFlags.WriteToCluster,
                NodeRole.Repeater or NodeRole.Backup => EventBusFlags.ReadFromCluster,
                NodeRole.Unassigned => EventBusFlags.None,
                _ => throw new ArgumentOutOfRangeException()
            }, id) { }

        public EventBus(EventBusFlags flags, int id = (int)StateID.CustomEvents)
        {
            k_DataId = id;
            if (flags.HasFlag(EventBusFlags.ReadFromCluster))
            {
                RepeaterStateReader.RegisterOnLoadDataDelegate(k_DataId, DeserializeAndPublish);
            }

            if (flags.HasFlag(EventBusFlags.WriteToCluster))
            {
                EmitterStateWriter.RegisterOnStoreCustomDataDelegate(k_DataId, SerializeAndFlush);
            }

            if (flags.HasFlag(EventBusFlags.Loopback))
            {
                ClusterSyncLooper.onInstancePostFrame += PublishLoopbackEvents;
            }
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

        void InvokeListeners(ReadOnlySpan<TEvent> events)
        {
            foreach (var item in events)
            {
                foreach (var listener in m_Listeners)
                {
                    listener.Invoke(item);
                }
            }

            foreach (var bulkListener in m_BulkListeners)
            {
                bulkListener.Invoke(events);
            }
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
                // Check the type id before deserializing
                var id = rawData.LoadStruct<IdType>();
                if (id != s_EventTypeId)
                {
                    return false;
                }

                var dataStart = Marshal.SizeOf<IdType>();
                var dataSegment = rawData.Slice(dataStart);
                var data = MemoryMarshal.Cast<byte, TEvent>(dataSegment);
                InvokeListeners(data);

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
        /// <remarks>
        /// You will not typically call this directly. This method is registered with the low-level
        /// cluster data API and will be invoked automatically when running in a cluster.
        /// </remarks>
        /// <param name="outBuffer">The buffer to hold the serialized events for transport.</param>
        /// <returns>The number of bytes copied to <paramref name="outBuffer"/>></returns>
        public int SerializeAndFlush(NativeArray<byte> outBuffer)
        {
            // Store the type identifier
            var bytesWritten = s_EventTypeId.StoreInBuffer(outBuffer);

            // Blit the data into the output buffer.
            var srcBytes = MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsSpan());
            var destBytes = outBuffer.GetSubArray(bytesWritten, outBuffer.Length - bytesWritten);

            bytesWritten += srcBytes.TryCopyTo(destBytes.AsSpan()) ? srcBytes.Length : 0;

            m_InBufferLength = m_OutBufferLength;
            m_OutBufferLength = 0;
            (m_OutBuffer, m_LoopbackBuffer) = (m_LoopbackBuffer, m_OutBuffer);
            return bytesWritten;
        }

        internal void PublishLoopbackEvents()
        {
            InvokeListeners(m_LoopbackBuffer.GetSubArray(0, m_InBufferLength).AsSpan());
        }

        /// <summary>
        /// Unregister with the cluster data API.
        /// </summary>
        public void Dispose()
        {
            EmitterStateWriter.UnregisterCustomDataDelegate(k_DataId, SerializeAndFlush);
            RepeaterStateReader.UnregisterOnLoadDataDelegate(k_DataId, DeserializeAndPublish);
            ClusterSyncLooper.onInstancePostFrame -= PublishLoopbackEvents;
            m_OutBuffer.Dispose();
        }
    }
}
