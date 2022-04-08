using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.Tests
{
    public class TestUtilities
    {
        [Test]
        public void TestBitVectorInitWithBits([Random(0, ulong.MaxValue, 8)] ulong bits)
        {
            Assert.That(BitVector.Length, Is.EqualTo(64));
            Assert.That(new BitVector().Bits, Is.EqualTo(0ul));
            var bv = new BitVector(bits);
            Assert.That(bv.Bits, Is.EqualTo(bits));
        }

        [Test]
        public void TestBitVectorInitWithIndex([Random(0, sizeof(ulong) * 8, 8)] int index)
        {
            var bv = BitVector.FromIndex(index);
            Assert.That(bv.Any(), Is.True);
            Assert.That(bv.Bits, Is.EqualTo(1ul << index));
        }

        [Test]
        public void TestBitVectorIndexing(
            [Random(0, ulong.MaxValue, 8)] ulong bits,
            [Random(0, sizeof(ulong) * 8, 8)] int index)
        {
            var bv = new BitVector(bits);
            var ones = BitVector.Ones;
            Assert.That(bv[index], Is.EqualTo(0 != (bits & (1ul << index))));
            Assert.That(ones[index], Is.True);
        }

        [Test]
        public void TestBitVectorEquality([Random(0, ulong.MaxValue, 8)] ulong bits)
        {
            var bv1 = new BitVector(bits);
            var bv2 = new BitVector(bits);
            var bv3 = bv1;
            var bv4 = new BitVector(bits + 1);
            Assert.That(bv1, Is.EqualTo(bv1));
            Assert.That(bv1, Is.EqualTo(bv2));
            Assert.That(bv1, Is.EqualTo(bv3));
            Assert.That(bv1, Is.Not.EqualTo(bv4));
        }

        [Test]
        public void TestBitVectorSetBit(
            [Random(0, ulong.MaxValue, 8)] ulong bits,
            [Random(0, sizeof(ulong) * 8, 8)] int index)
        {
            var bv = new BitVector(bits).SetBit(index);
            Assert.That(bv[index], Is.True);
        }

        [Test]
        public void TestBitVectorUnsetBit(
            [Random(0, ulong.MaxValue, 8)] ulong bits,
            [Random(0, sizeof(ulong) * 8, 8)] int index)
        {
            var bv = new BitVector(bits).UnsetBit(index);
            Assert.That(bv[index], Is.False);
        }
        
        [Test]
        public void TestBitVectorMask(
            [Random(0, ulong.MaxValue, 8)] ulong bits,
            [Random(0, ulong.MaxValue, 8)] ulong mask)
        {
            Assert.That(new BitVector(bits).MaskBits(new BitVector(mask)).Bits, Is.EqualTo(bits & mask));
        }
    }
}
