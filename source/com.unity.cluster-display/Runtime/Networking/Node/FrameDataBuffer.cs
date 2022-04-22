using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Unity.ClusterDisplay.NetworkingHelpers;

namespace Unity.ClusterDisplay
{
    class FrameDataBuffer : IDisposable
    {
        NativeArray<byte> m_Data;

        public bool IsValid => m_Data.IsCreated;
        public int Length { get; private set; }

        public delegate int CustomDataDelegate(NativeArray<byte> writeableBuffer);

        public IEnumerable<byte> Data => m_Data.GetSubArray(0, Length);

        public FrameDataBuffer(int capacity)
        {
            m_Data = new NativeArray<byte>(capacity, Allocator.Persistent);
            Length = 0;
        }

        public void Dispose()
        {
            m_Data.Dispose();
        }

        public void CopyTo(Span<byte> dest,
            int offset = 0) =>
            m_Data.GetSubArray(0, Length)
                .AsSpan()
                .CopyTo(dest.Slice(offset));

        public void Clear()
        {
            Length = 0;
        }

        public void Store(byte id, CustomDataDelegate saveFunc)
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizePos = Length;
            var sizeBytes = Marshal.SizeOf<int>();

            var dataStart = sizePos + sizeBytes;
            var length = m_Data.Length - dataStart;
            var dataSize = saveFunc(m_Data.GetSubArray(dataStart, length));

            if (dataSize <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            Length += dataSize.StoreInBuffer(m_Data, sizePos);
            Length += dataSize;
        }

        public void Store(StateID stateId)
        {
            var idByte = (byte)stateId;
            Length += idByte.StoreInBuffer(m_Data, Length);

            var sizeBytes = 0;
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
        }

        public void Store<T>(byte id, ref T data) where T : unmanaged
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = Marshal.SizeOf<int>();
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
            Length += data.StoreInBuffer(m_Data, Length);
        }
    }
}
