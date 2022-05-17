using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class EventBus<T> : IDisposable where T : unmanaged
    {
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

        public bool Publish(T eventData)
        {
            if (m_OutBufferLength < m_OutBuffer.Length - 1)
            {
                m_OutBuffer[m_OutBufferLength++] = eventData;
                return true;
            }

            return false;
        }

        public IDisposable Subscribe(Action<T> listener)
        {
            m_Listeners.Add(listener);
            return new Unsubscriber<Action<T>>(m_Listeners, listener);
        }

        public IDisposable Subscribe(BulkEventListener bulkListener)
        {
            m_BulkListeners.Add(bulkListener);
            return new Unsubscriber<BulkEventListener>(m_BulkListeners, bulkListener);
        }

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


        public bool DeserializeAndPublish(NativeArray<byte> rawData) => DeserializeAndPublish(rawData.AsReadOnlySpan());

        int SerializeAndFlush(NativeArray<byte> outData)
        {
            var bytes = MemoryMarshal.AsBytes(m_OutBuffer.GetSubArray(0, m_OutBufferLength).AsSpan());
            var bytesWritten = bytes.TryCopyTo(outData.AsSpan()) ? bytes.Length : 0;
            m_OutBufferLength = 0;
            return bytesWritten;
        }

        public void Dispose()
        {
            EmitterStateWriter.UnregisterCustomDataDelegate(k_FrameDataId);
            RepeaterStateReader.UnregisterOnLoadDataDelegate(k_FrameDataId);
        }
    }
}
