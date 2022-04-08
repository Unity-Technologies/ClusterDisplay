using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// Convenience class for accessing a 64-bit unsigned value as if it were
    /// a vector of 64 bits.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly struct BitVector : IEquatable<BitVector>
    {
        public ulong Bits { get; }
        
        internal BitVector(ulong bits) => Bits = bits;
        
        public static BitVector FromIndex(int index) => new (1ul << index);
        public static readonly BitVector Ones = new (~0ul);
        
        public bool this[int index] => (Bits & (1ul << index)) != 0;
        public static int Length => sizeof(ulong) * 8;

        public override string ToString()
        {
            return Convert.ToString((long) Bits, 2);
        }

        public bool Equals(BitVector other)
        {
            return Bits == other.Bits;
        }

        public override bool Equals(object obj)
        {
            return obj is BitVector other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Bits.GetHashCode();
        }

        public static bool operator ==(BitVector left, BitVector right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BitVector left, BitVector right)
        {
            return !left.Equals(right);
        }
    }

    static class BitVectorOperations
    {
        public static BitVector SetBit(this BitVector bitVector,
            int index) =>
            new(bitVector.Bits | (1ul << index));

        public static BitVector UnsetBit(this BitVector bitVector,
            int index) =>
            new(bitVector.Bits & ~(1ul << index));

        public static BitVector MaskBits(this BitVector bitVector, BitVector mask) => new(bitVector.Bits & mask.Bits);
        public static bool Any(this BitVector bitVector) => bitVector.Bits != 0;
    }
}
