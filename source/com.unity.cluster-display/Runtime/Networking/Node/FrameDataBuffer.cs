using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class FrameDataBuffer : IDisposable
    {
        NativeArray<byte> m_Data;

        bool m_Disposed;

        public bool IsValid => m_Data.IsCreated && !m_Disposed;
        public int Length { get; private set; }

        public delegate int StoreDataDelegate(NativeArray<byte> writeableBuffer);

        public NativeArray<byte>.ReadOnly Data => m_Data.GetSubArray(0, Length).AsReadOnly();

        public FrameDataBuffer(int capacity = ushort.MaxValue)
        {
            m_Data = new NativeArray<byte>(capacity, Allocator.Persistent);
            m_Disposed = false;
            Length = 0;
        }

        public void Dispose()
        {
            if (!m_Disposed)
                m_Data.Dispose();

            m_Disposed = true;
        }

        public void CopyTo(Span<byte> dest, int offset = 0) =>
            m_Data.GetSubArray(0, Length)
                .AsSpan()
                .CopyTo(dest.Slice(offset));

        public void Clear() => Length = 0;

        public void Store(byte id, StoreDataDelegate saveFunc)
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizePos = Length;
            var sizeBytes = Marshal.SizeOf<int>();

            var dataStart = sizePos + sizeBytes;
            var length = m_Data.Length - dataStart;
            var dataSize = saveFunc(m_Data.GetSubArray(dataStart, length));

            if (dataSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(saveFunc), "Serialization callback failed (returned negative value).");
            }

            Length += dataSize.StoreInBuffer(m_Data, sizePos);
            Length += dataSize;
        }

        public void Store(byte id)
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = 0;
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
        }

        public void Store<T>(byte id, ref T data) where T : unmanaged
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = Marshal.SizeOf<T>();
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
            Length += data.StoreInBuffer(m_Data, Length);
        }
    }

    struct FrameDataReader
    {
        NativeArray<byte> m_Data;

        public FrameDataReader(NativeArray<byte> data)
        {
            m_Data = data;
        }

        public FrameDataEnumerator GetEnumerator() => new(m_Data);
    }

    struct FrameDataEnumerator
    {
        NativeArray<byte> m_Data;
        int m_ReadHead;
        byte m_CurrentId;
        int m_CurrentBlockSize;
        static readonly int k_IdSize = Marshal.SizeOf<byte>();
        static readonly int k_MetadataSize = Marshal.SizeOf<byte>() + Marshal.SizeOf<int>();

        public FrameDataEnumerator(NativeArray<byte> bufferData)
        {
            m_Data = bufferData;
            m_ReadHead = -k_MetadataSize;
            m_CurrentBlockSize = 0;
            m_CurrentId = 0;
        }

        public (byte id, NativeArray<byte> data) Current =>
            (m_CurrentId, m_Data.GetSubArray(m_ReadHead + k_MetadataSize, m_CurrentBlockSize));

        public bool MoveNext()
        {
            m_ReadHead += k_MetadataSize + m_CurrentBlockSize;
            m_CurrentId = m_Data.LoadStruct<byte>(m_ReadHead);
            m_CurrentBlockSize = m_Data.LoadStruct<int>(m_ReadHead + k_IdSize);
            return m_CurrentId != (byte) StateID.End;
        }
    }
}
