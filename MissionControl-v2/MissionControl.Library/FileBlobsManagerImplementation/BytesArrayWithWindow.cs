using System;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Defines the range in an array where we have data
    /// </summary>
    class BytesArrayWithWindow
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="capacity">Capacity of the bytes array (in bytes).</param>
        public BytesArrayWithWindow(int capacity)
        {
            m_Storage = new byte[capacity];
        }

        /// <summary>
        /// Area of the array that contains data.
        /// </summary>
        public ReadOnlyMemory<byte> CurrentData => new ReadOnlyMemory<byte>(m_Storage, m_First, m_Length);

        /// <summary>
        /// Returns access to the storage array
        /// </summary>
        public byte[] Storage => m_Storage;

        /// <summary>
        /// Returns the index in <see cref="Storage"/> where <see cref="CurrentData"/> starts.
        /// </summary>
        public int CurrentDataIndexInStorage => m_First;

        /// <summary>
        /// Number of bytes with data in the array.
        /// </summary>
        public int CurrentDataLength => m_Length;

        /// <summary>
        /// Area of the array that can be filled with new data to continue with whatever we currently have.
        /// </summary>
        public Memory<byte> RemainingBuffer =>
            new Memory<byte>(m_Storage, m_First + m_Length, m_Storage.Length - (m_First + m_Length));

        /// <summary>
        /// Indicates that the first <paramref name="length"/> bytes of <see cref="RemainingBuffer"/> have been
        /// filled with new data (and so, <see cref="CurrentData"/> is <paramref name="length"/> bytes larger than it
        /// was).
        /// </summary>
        /// <param name="length">Length of the new data.</param>
        public void GrowCurrentData(int length)
        {
            if (m_First + m_Length + length > m_Storage.Length)
            {
                throw new OverflowException($"Adding {length} new bytes to BytesArrayWithWindow would cause an " +
                    $"overflow.");
            }
            m_Length += length;
        }

        /// <summary>
        /// Indicate that the <paramref name="length"/> first bytes of <see cref="CurrentData"/> have been consumed.
        /// </summary>
        /// <param name="length">Length of the consumed data.</param>
        public void IndicateDataConsumed(int length)
        {
            if (length > m_Length)
            {
                throw new OverflowException($"Consuming {length} bytes while BytesArrayWithWindow only has " +
                    $"{m_Length} bytes with data.");
            }
            m_Length -= length;
            if (m_Length > 0)
            {
                m_First += length;
            }
            else
            {
                m_First = 0;
            }
        }

        byte[] m_Storage;
        int m_First;
        int m_Length;
    }
}
