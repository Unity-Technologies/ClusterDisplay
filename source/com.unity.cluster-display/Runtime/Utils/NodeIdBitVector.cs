using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Read-only portion of <see cref="NodeIdBitVector"/>.
    /// </summary>
    class NodeIdBitVectorReadOnly
    {
        /// <summary>
        /// Default constructor initializing all bits to 0 (<c>false</c>).
        /// </summary>
        public NodeIdBitVectorReadOnly()
        {
        }

        /// <summary>
        /// Constructor copying bits value from another <see cref="NodeIdBitVectorReadOnly"/>.
        /// </summary>
        /// <param name="initialBits">To copy bits value from.</param>
        public NodeIdBitVectorReadOnly(NodeIdBitVectorReadOnly initialBits)
        {
            initialBits.CopyTo(m_Storage);
            m_SetBits = initialBits.m_SetBits;
        }

        /// <summary>
        /// Constructor setting the bits from a list bits to set.
        /// </summary>
        /// <param name="toSet">Index of the bits to set.</param>
        public NodeIdBitVectorReadOnly(IEnumerable<byte> toSet)
        {
            foreach (var nodeId in toSet)
            {
                if (!this[nodeId])
                {
                    var storageIndex = nodeId >> 6;
                    ulong bitMask = 1ul << (nodeId & 0b11_1111);
                    if ((m_Storage[storageIndex].Value & bitMask) == 0)
                    {
                        m_Storage[storageIndex].Value |= bitMask;
                        ++m_SetBits;
                    }
                }
            }
        }

        /// <summary>
        /// Constructor setting the bits from 4 ulong where their bits indicate if the corresponding node id bit is set.
        /// </summary>
        /// <param name="from0">Contains the bits 0 to 63 indicating if bit is set for a node id 0 to 63.</param>
        /// <param name="from1">Contains the bits 64 to 127 indicating if bit is set for a node id 64 to 127.</param>
        /// <param name="from2">Contains the bits 128 to 191 indicating if bit is set for a node id 128 to 191.</param>
        /// <param name="from3">Contains the bits 192 to 255 indicating if bit is set for a node id 192 to 255.</param>
        public NodeIdBitVectorReadOnly(ulong from0, ulong from1, ulong from2, ulong from3)
        {
            m_Storage[0].Value = from0;
            m_Storage[1].Value = from1;
            m_Storage[2].Value = from2;
            m_Storage[3].Value = from3;
            m_SetBits = m_Storage[0].CountBits() + m_Storage[1].CountBits() + m_Storage[2].CountBits() +
                m_Storage[3].CountBits();
        }

        /// <summary>
        /// Get or set bits in the <see cref="NodeIdBitVector"/>.
        /// </summary>
        /// <param name="nodeId">Index of the bit to get or set (corresponding to a NodeId).</param>
        public bool this[byte nodeId] => m_Storage[nodeId >> 6].IsSet(nodeId & 0b11_1111);

        /// <summary>
        /// Number of bits set to 1 / <c>true</c>.
        /// </summary>
        public int SetBitsCount => m_SetBits;

        /// <summary>
        /// Copy all the bits contained in this vector to the given destination.
        /// </summary>
        /// <param name="dest">ulong[4]</param>
        public void CopyTo(ulong[] dest)
        {
            dest[0] = m_Storage[0].Value;
            dest[1] = m_Storage[1].Value;
            dest[2] = m_Storage[2].Value;
            dest[3] = m_Storage[3].Value;
        }

        /// <summary>
        /// Copy all the bits contained in this vector to 4 ulong.
        /// </summary>
        /// <param name="dest0">Filled with the bits 0 to 63 indicating if bit is set for a node id 0 to 63.</param>
        /// <param name="dest1">Filled with the bits 64 to 127 indicating if bit is set for a node id 64 to 127.</param>
        /// <param name="dest2">Filled with the bits 128 to 191 indicating if bit is set for a node id 128 to 191.</param>
        /// <param name="dest3">Filled with the bits 192 to 255 indicating if bit is set for a node id 192 to 255.</param>
        public void CopyTo(out ulong dest0, out ulong dest1, out ulong dest2, out ulong dest3)
        {
            dest0 = m_Storage[0].Value;
            dest1 = m_Storage[1].Value;
            dest2 = m_Storage[2].Value;
            dest3 = m_Storage[3].Value;
        }

        /// <summary>
        /// Copy all the bits contained in this vector to the given destinationb
        /// </summary>
        /// <param name="dest">BitField64[4]</param>
        public void CopyTo(BitField64[] dest)
        {
            dest[0] = m_Storage[0];
            dest[1] = m_Storage[1];
            dest[2] = m_Storage[2];
            dest[3] = m_Storage[3];
        }

        /// <summary>
        /// Go through every bits and extract the NodeId of every nodes that are true.
        /// </summary>
        /// <returns>Heap allocated array of NodeId.</returns>
        public byte[] ExtractSetBits()
        {
            byte[] ret = new byte[m_SetBits];
            byte retSetIndex = 0;
            void AppendToRet(BitField64 bitField64, byte firstIndex)
            {
                if (bitField64.Value != 0)
                {
                    for (int bitIndex = 0; bitIndex < 64; ++bitIndex)
                    {
                        if (bitField64.IsSet(bitIndex))
                        {
                            ret[retSetIndex] = (byte)(firstIndex + bitIndex);
                            ++retSetIndex;
                        }
                    }
                }
            }
            AppendToRet(m_Storage[0], 0);
            AppendToRet(m_Storage[1], 64);
            AppendToRet(m_Storage[2], 128);
            AppendToRet(m_Storage[3], 192);
            return ret;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            bool firstSetBit = true;
            void AppendBitfield64(BitField64 bitField64, byte firstIndex)
            {
                if (bitField64.Value != 0)
                {
                    for (int bitIndex = 0; bitIndex < 64; ++bitIndex)
                    {
                        if (bitField64.IsSet(bitIndex))
                        {
                            if (firstSetBit)
                            {
                                firstSetBit = false;
                            }
                            else
                            {
                                builder.Append(", ");
                            }
                            builder.Append(firstIndex + bitIndex);
                        }
                    }
                }
            }

            AppendBitfield64(m_Storage[0], 0);
            AppendBitfield64(m_Storage[1], 64);
            AppendBitfield64(m_Storage[2], 128);
            AppendBitfield64(m_Storage[3], 192);
            return builder.ToString();
        }

        /// <summary>
        /// Set all the bits from the given <see cref="NodeIdBitVectorReadOnly"/> (if they are not already set).
        /// </summary>
        /// <param name="from">Bits to set.</param>
        /// <remarks>I know, setting bits is not really readonly, but we cannot put it in <see cref="NodeIdBitVector"/>
        /// as it needs to access <c>from.m_Storage</c>.  So protected in this class and <see cref="NodeIdBitVector"/>
        /// make it accessible to the public.</remarks>
        protected void Set(NodeIdBitVectorReadOnly from)
        {
            for (int bitField64Index = 0; bitField64Index < 4; ++bitField64Index)
            {
                if (from.m_Storage[bitField64Index].Value != 0)
                {
                    int setBitsBefore = m_Storage[bitField64Index].CountBits();
                    m_Storage[bitField64Index].Value |= from.m_Storage[bitField64Index].Value;
                    int setBitsAfter = m_Storage[bitField64Index].CountBits();
                    Debug.Assert(setBitsAfter >= setBitsBefore);
                    m_SetBits += (setBitsAfter - setBitsBefore);
                }
            }
        }

        /// <summary>
        /// Clear all the bits from the given <see cref="NodeIdBitVectorReadOnly"/> (if they are not already cleared).
        /// </summary>
        /// <param name="from">Bits to clear.</param>
        /// <remarks>I know, clearing bits is not really readonly, but we cannot put it in <see cref="NodeIdBitVector"/>
        /// as it needs to access <c>from.m_Storage</c>.  So protected in this class and <see cref="NodeIdBitVector"/>
        /// make it accessible to the public.</remarks>
        protected void Clear(NodeIdBitVectorReadOnly from)
        {
            for (int bitField64Index = 0; bitField64Index < 4; ++bitField64Index)
            {
                if (from.m_Storage[bitField64Index].Value != 0)
                {
                    int setBitsBefore = m_Storage[bitField64Index].CountBits();
                    m_Storage[bitField64Index].Value &= ~from.m_Storage[bitField64Index].Value;
                    int setBitsAfter = m_Storage[bitField64Index].CountBits();
                    Debug.Assert(setBitsAfter <= setBitsBefore);
                    m_SetBits -= (setBitsBefore - setBitsAfter);
                }
            }
        }

        /// <summary>
        /// Storage for the bits necessary to store a boolean for potential nodes.
        /// </summary>
        protected BitField64[] m_Storage = new BitField64[4];
        /// <summary>
        /// How many bits in <see cref="m_Storage"/> are set to 1.
        /// </summary>
        protected int m_SetBits;
    }

    /// <summary>
    /// Small helper to store a bit for each potential NodeId (<see cref="byte"/>).
    /// </summary>
    class NodeIdBitVector: NodeIdBitVectorReadOnly
    {
        /// <summary>
        /// Default constructor initializing all bits to 0 (<c>false</c>).
        /// </summary>
        public NodeIdBitVector()
        {
        }

        /// <summary>
        /// Constructor copying bits value from another <see cref="NodeIdBitVectorReadOnly"/>.
        /// </summary>
        /// <param name="initialBits">To copy bits value from.</param>
        public NodeIdBitVector(NodeIdBitVectorReadOnly initialBits)
            : base(initialBits) { }

        /// <summary>
        /// Constructor setting the bits from a list bits to set.
        /// </summary>
        /// <param name="toSet">Index of the bits to set.</param>
        public NodeIdBitVector(IEnumerable<byte> toSet)
            : base(toSet) { }

        /// <summary>
        /// Constructor setting the bits from 4 ulong where their bits indicate if the corresponding node id bit is set.
        /// </summary>
        /// <param name="from0">Contains the bits 0 to 63 indicating if bit is set for a node id 0 to 63.</param>
        /// <param name="from1">Contains the bits 64 to 127 indicating if bit is set for a node id 64 to 127.</param>
        /// <param name="from2">Contains the bits 128 to 191 indicating if bit is set for a node id 128 to 191.</param>
        /// <param name="from3">Contains the bits 192 to 255 indicating if bit is set for a node id 192 to 255.</param>
        public NodeIdBitVector(ulong from0, ulong from1, ulong from2, ulong from3)
            : base(from0, from1, from2, from3) { }

        /// <summary>
        /// Get or set bits in the <see cref="NodeIdBitVector"/>.
        /// </summary>
        /// <param name="nodeId">Index of the bit to get or set (corresponding to a NodeId).</param>
        public new bool this[byte nodeId]
        {
            get => base[nodeId];
            set
            {
                var storageIndex = nodeId >> 6;
                ulong bitMask = 1ul << (nodeId & 0b11_1111);
                if (value)
                {
                    if ((m_Storage[storageIndex].Value & bitMask) == 0)
                    {
                        m_Storage[storageIndex].Value |= bitMask;
                        ++m_SetBits;
                    }
                }
                else
                {
                    if ((m_Storage[storageIndex].Value & bitMask) != 0)
                    {
                        m_Storage[storageIndex].Value &= ~bitMask;
                        --m_SetBits;
                    }
                }
            }
        }

        /// <summary>
        /// Sets the bits from the given <see cref="NodeIdBitVectorReadOnly"/>.
        /// </summary>
        /// <param name="from"></param>
        public void SetFrom(NodeIdBitVectorReadOnly from)
        {
            from.CopyTo(m_Storage);
            m_SetBits = from.SetBitsCount;
        }

        /// <summary>
        /// Set all the bits from the given <see cref="NodeIdBitVectorReadOnly"/> (if they are not already set).
        /// </summary>
        /// <param name="from">Bits to set.</param>
        public new void Set(NodeIdBitVectorReadOnly from) => base.Set(from);

        /// <summary>
        /// Clear all the bits from the given <see cref="NodeIdBitVectorReadOnly"/> (if they are not already cleared).
        /// </summary>
        /// <param name="from">Bits to clear.</param>
        public new void Clear(NodeIdBitVectorReadOnly from) => base.Clear(from);
    }
}
