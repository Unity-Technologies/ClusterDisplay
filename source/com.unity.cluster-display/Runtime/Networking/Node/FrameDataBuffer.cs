using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class that holds serialized frame data.
    /// </summary>
    /// <remarks>
    /// Encapsulates a <see cref="NativeArray{T}"/> of bytes. Data consists of a
    /// contiguous chain of data blocks. The type of data contained in each block
    /// should be identified by its ID. The chain can be terminated by writing a block
    /// with type ID of <see cref="StateID.End"/>.
    /// </remarks>
    class FrameDataBuffer : IDisposable
    {
        NativeArray<byte> m_Data;

        bool m_Disposed;

        public bool IsValid => m_Data.IsCreated && !m_Disposed;
        public int Length { get; private set; }

        public int Capacity => IsValid ? m_Data.Length : 0;

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

        /// <summary>
        /// Write a block of data using a custom serialization delegate.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        /// <param name="saveFunc">Callback function that writes data to the buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the buffer is not large enough to hold the data to be written.
        /// </exception>
        /// <remarks>
        /// The callback <paramref name="saveFunc"/> takes a <see cref="NativeArray{T}"/> argument,
        /// which represents the memory that it may write too. The callback shall return the
        /// number of bytes that it wrote (consumed).
        /// </remarks>
        public void Store(int id, StoreDataDelegate saveFunc)
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

        /// <summary>
        /// Write a data block consisting of an ID and no other data.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        public void Store(int id)
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = 0;
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
        }

        /// <summary>
        /// Write a block with the byte representation of <paramref name="data"/>.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        /// <param name="data">A value type instance.</param>
        /// <typeparam name="T"></typeparam>
        public void Store<T>(int id, ref T data) where T : unmanaged
        {
            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = Marshal.SizeOf<T>();
            Length += sizeBytes.StoreInBuffer(m_Data, Length);
            Length += data.StoreInBuffer(m_Data, Length);
        }
    }

    /// <summary>
    /// A non-allocating enumerator for serialized frame data blocks contained in a <see cref="NativeArray{T}"/>.
    /// </summary>
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
        int m_CurrentId;
        int m_CurrentBlockSize;
        static readonly int k_IdSize = Marshal.SizeOf<int>();
        static readonly int k_MetadataSize = k_IdSize + Marshal.SizeOf<int>();

        public FrameDataEnumerator(NativeArray<byte> bufferData)
        {
            m_Data = bufferData;
            m_ReadHead = -k_MetadataSize;
            m_CurrentBlockSize = 0;
            m_CurrentId = 0;
        }

        public (int id, NativeArray<byte> data) Current =>
            (m_CurrentId, m_Data.GetSubArray(m_ReadHead + k_MetadataSize, m_CurrentBlockSize));

        public bool MoveNext()
        {
            m_ReadHead += k_MetadataSize + m_CurrentBlockSize;
            if (m_ReadHead >= m_Data.Length)
            {
                return false;
            }
            m_CurrentId = m_Data.LoadStruct<int>(m_ReadHead);
            m_CurrentBlockSize = m_Data.LoadStruct<int>(m_ReadHead + k_IdSize);
            return m_CurrentId != (int) StateID.End;
        }
    }
}
