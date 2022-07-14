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
        NativeArray<byte> m_Data = new(ushort.MaxValue, Allocator.Persistent);

        bool m_Disposed;

        bool m_IsFrozen;

        /// <summary>
        /// Is the data contained in the <see cref="FrameDataBuffer"/> frozen.
        /// </summary>
        /// <remarks>Data will be frozen once the <see cref="Data"/> or <see cref="DataSpan"/> methods are called.  It
        /// will remain frozen until the <see cref="Clear"/> method is called.</remarks>
        public bool IsFrozen => m_IsFrozen;

        public bool IsValid => m_Data.IsCreated && !m_Disposed;
        public int Length { get; private set; }

        public int Capacity => IsValid ? m_Data.Length : 0;

        public delegate int StoreDataDelegate(NativeArray<byte> writeableBuffer);

        /// <summary>
        /// Access to the data contained in the <see cref="FrameDataBuffer"/>.
        /// </summary>
        /// <remarks>Calling this property has the side effect of freezing the <see cref="FrameDataBuffer"/> so that
        /// any call to any method modifying the data will fail until the <see cref="Clear"/> method is called.</remarks>
        public NativeArray<byte>.ReadOnly Data()
        {
            return DataSpan(0, Length);
        }

        /// <summary>
        /// Access to a part of the the data contained in the <see cref="FrameDataBuffer"/>.
        /// </summary>
        /// <remarks>Calling this property has the side effect of freezing the <see cref="FrameDataBuffer"/> so that
        /// any call to any method modifying the data will fail until the <see cref="Clear"/> method is called.</remarks>
        public NativeArray<byte>.ReadOnly DataSpan(int start, int length)
        {
            m_IsFrozen = true;
            return m_Data.GetSubArray(start, length).AsReadOnly();
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

        /// <summary>
        /// Clear the content of the <see cref="FrameDataBuffer"/> as if it was brand new except it keeps the internally
        /// allocated memory.  This mean that the data returned by <see cref="Data"/> or <see cref="DataSpan"/> cannot
        /// be considered valid anymore.
        /// </summary>
        public void Clear()
        {
            m_IsFrozen = false;
            Length = 0;
        }

        /// <summary>
        /// Write a block of data using a custom serialization delegate.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        /// <param name="saveFunc">Callback function that writes data to the buffer.</param>
        /// <exception cref="OutOfMemoryException">If trying to grow the <see cref="FrameDataBuffer"/> larger than the
        /// maximum size.</exception>
        /// <exception cref="InvalidOperationException"> Thrown if the <see cref="FrameDataBuffer"/> is frozen (see
        /// <see cref="IsFrozen"/>)"/>.</exception>
        /// <remarks>
        /// The callback <paramref name="saveFunc"/> takes a <see cref="NativeArray{T}"/> argument,
        /// which represents the memory that it may write too. The callback shall return the
        /// number of bytes that it wrote (consumed).
        /// </remarks>
        public void Store(int id, StoreDataDelegate saveFunc)
        {
            if (m_IsFrozen)
            {
                throw new InvalidOperationException("Cannot call the store method on a frozen FrameDataBuffer.");
            }

            EnsureHasEnoughCapacity(Length + k_EntryBytes);

            Length += id.StoreInBuffer(m_Data, Length);
            var sizePos = Length;
            var sizeBytes = Marshal.SizeOf<int>();

            int dataSize;
            do
            {
                var dataStart = sizePos + sizeBytes;
                var availableSpace = m_Data.Length - dataStart;
                dataSize = saveFunc(m_Data.GetSubArray(dataStart, availableSpace));

                if (dataSize < 0)
                {
                    // Since we do not know in advance how much storage saveFunc needs, just increase storage by 1 step.
                    EnsureHasEnoughCapacity(Capacity + 1);
                }
                else if (dataSize > availableSpace)
                {
                    throw new ArgumentOutOfRangeException(nameof(saveFunc), "saveFunc written (or claim to have written) more data to the buffer than what can fit in it.");
                }
            } while (dataSize < 0);

            Length += dataSize.StoreInBuffer(m_Data, sizePos);
            Length += dataSize;
        }

        /// <summary>
        /// Write a data block consisting of an ID and no other data.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        /// <exception cref="OutOfMemoryException">If trying to grow the <see cref="FrameDataBuffer"/> larger than the
        /// maximum size.</exception>
        /// <exception cref="InvalidOperationException"> Thrown if the <see cref="FrameDataBuffer"/> is frozen (see
        /// <see cref="IsFrozen"/>)"/>.</exception>
        public void Store(int id)
        {
            if (m_IsFrozen)
            {
                throw new InvalidOperationException("Cannot call the store method on a frozen FrameDataBuffer.");
            }

            int requiredCapacity = Length + k_EntryBytes;
            EnsureHasEnoughCapacity(requiredCapacity);

            Length += id.StoreInBuffer(m_Data, Length);

            var sizeBytes = 0;
            Length += sizeBytes.StoreInBuffer(m_Data, Length);

            Debug.Assert(Length == requiredCapacity);
        }

        /// <summary>
        /// Write a block with the byte representation of <paramref name="data"/>.
        /// </summary>
        /// <param name="id">The ID of the block type.</param>
        /// <param name="data">A value type instance.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="OutOfMemoryException">If trying to grow the <see cref="FrameDataBuffer"/> larger than the
        /// maximum size.</exception>
        /// <exception cref="InvalidOperationException"> Thrown if the <see cref="FrameDataBuffer"/> is frozen (see
        /// <see cref="IsFrozen"/>)"/>.</exception>
        public void Store<T>(int id, ref T data) where T : unmanaged
        {
            if (m_IsFrozen)
            {
                throw new InvalidOperationException("Cannot call the store method on a frozen FrameDataBuffer.");
            }

            var dataBytes = Marshal.SizeOf<T>();
            int requiredCapacity = Length + k_EntryBytes + dataBytes;
            EnsureHasEnoughCapacity(requiredCapacity);

            Length += id.StoreInBuffer(m_Data, Length);

            Length += dataBytes.StoreInBuffer(m_Data, Length);
            Length += data.StoreInBuffer(m_Data, Length);

            Debug.Assert(Length == requiredCapacity);
        }

        /// <summary>
        /// Ensure the storage of the <see cref="FrameDataBuffer"/> can at least store the requested amount of data.
        /// </summary>
        /// <param name="requiredCapacity">Minimum capacity needed.</param>
        /// <exception cref="OutOfMemoryException">If trying to grow the <see cref="FrameDataBuffer"/> larger than the
        /// maximum size.</exception>
        void EnsureHasEnoughCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= Capacity)
            {
                return;
            }

            int newCapacity;
            do
            {
                newCapacity = Math.Min(Capacity * 2, k_MaximumCapacity);
                if (newCapacity == Capacity)
                {
                    throw new OutOfMemoryException($"Trying to grow a FrameDataBuffer larger than the maximum of {k_MaximumCapacity} bytes.");
                }
            } while (newCapacity < requiredCapacity);

            var newStorage = new NativeArray<byte>(newCapacity, Allocator.Persistent);
            NativeArray<byte>.Copy(m_Data, newStorage, Length);
            m_Data.Dispose();
            m_Data = newStorage;
        }

        /// <summary>
        /// Maximum size of data in a FrameDataBuffer.  Why 1 meg?  1 meg * 8 bits * 60 frames per second is over 500
        /// mbps, which is quite a lot.  Users should keep the amount of memory data transferred between nodes
        /// reasonable. In case a burst of data need to be send then that buts must be distributed over multiple frames
        /// as such a high amount of data would require a lot of processing causing an unstable framerate.
        /// </summary>
        const int k_MaximumCapacity = 1024 * 1024;
        /// <summary>
        /// Size in bytes of the smallest entry possible in the <see cref="FrameDataBuffer"/>.
        /// </summary>
        const int k_EntryBytes = 8; // 4 for the id and 4 for the size of the entry.
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
