using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Unity.ClusterDisplay.NetworkingHelpers;

namespace Unity.ClusterDisplay
{
    struct StateBuffer : IDisposable
    {
        NativeArray<byte> m_StateData;
        int m_Length;

        public bool IsValid => m_StateData.IsCreated;
        public int Length => m_Length;

        public Span<byte> BeginWrite() => m_StateData.GetSubArray(m_Length, m_StateData.Length - m_Length).AsSpan();

        public void EndWrite(int length) => m_Length += length;

        public IEnumerable<byte> Data => m_StateData.GetSubArray(0, m_Length);

        public StateBuffer(int capacity)
        {
            m_StateData = new NativeArray<byte>(capacity, Allocator.Persistent);
            m_Length = 0;
        }

        public void Dispose()
        {
            m_StateData.Dispose();
        }

        public bool TryCopyTo(Span<byte> dest,
            int offset = 0) =>
            m_StateData.GetSubArray(0, m_Length)
                .AsSpan()
                .TryCopyTo(dest.Slice(offset));

        public void Clear()
        {
            m_Length = 0;
        }

        public void StoreAllStates()
        {
            Clear();
            StoreState(StateID.Input);
            StoreState(StateID.Time);
            StoreState(StateID.Random);
        }

        public void StoreState(StateID stateID)
        {
            Func<NativeArray<byte>, int> saveFunc = stateID switch
            {
                StateID.Time => ClusterSerialization.SaveTimeManagerState,
                StateID.Input => ClusterSerialization.SaveInputManagerState,
                StateID.Random => StateBufferExtensions.SaveRandomState,
                StateID.ClusterInput => throw new InvalidOperationException("Deprecated"),
                StateID.End => StateBufferExtensions.MarkStateEnd,
                _ => throw new ArgumentOutOfRangeException(nameof(stateID), stateID, null)
            };

            var id = (byte)stateID;
            m_Length += id.StoreInBuffer(m_StateData, m_Length);

            var sizePos = m_Length;
            var sizeBytes = Marshal.SizeOf<int>();

            var dataStart = sizePos + sizeBytes;
            var length = m_StateData.Length - dataStart;
            var dataSize = saveFunc(m_StateData.GetSubArray(dataStart, length));

            if (dataSize <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            m_Length += dataSize.StoreInBuffer(m_StateData, sizePos);
            m_Length += dataSize;
        }
    }

    static class StateBufferExtensions
    {
        public static int SaveRandomState(NativeArray<byte> arr)
        {
            var state = UnityEngine.Random.state;
            return state.StoreInBuffer(arr);
        }

        public static int MarkStateEnd(NativeArray<byte> arr)
        {
            var val = (int)StateID.End;
            return val.StoreInBuffer(arr);
        }
    }
}
