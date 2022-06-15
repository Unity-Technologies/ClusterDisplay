using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using Unity.Collections;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// A blittable struct for testing data transfer functions.
    /// </summary>
    struct TestData
    {
        public StateID EnumVal;
        public long LongVal;
        public float FloatVal;
        public FixedString32Bytes Message;

        public override string ToString()
        {
            return $"[{EnumVal}, {LongVal}, {FloatVal}, {Message}]";
        }
    }

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

        interface IFoo
        {
            int Value { get; }
        }

        class Alpha : IFoo
        {
            public int Value => 5;
        }

        class Bravo : IFoo
        {
            public int Value => 10;
        }

        [Test]
        public void TestServiceLocator()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFoo>());
            Assert.IsFalse(ServiceLocator.TryGet<IFoo>(out _));

            var alpha = new Alpha();
            ServiceLocator.Provide<IFoo>(alpha);

            Assert.DoesNotThrow(() => ServiceLocator.Get<IFoo>());
            Assert.IsTrue(ServiceLocator.TryGet(out IFoo service));
            Assert.That(service, Is.EqualTo(ServiceLocator.Get<IFoo>()));
            Assert.That(service, Is.EqualTo(alpha));
            Assert.That(service.Value, Is.EqualTo(5));

            var bravo = new Bravo();
            Assert.That(alpha, Is.Not.EqualTo(bravo));
            ServiceLocator.Provide<IFoo>(bravo);

            Assert.IsTrue(ServiceLocator.TryGet(out service));
            Assert.That(service, Is.EqualTo(bravo));
            Assert.That(ServiceLocator.Get<IFoo>(), Is.EqualTo(bravo));
            Assert.That(service.Value, Is.EqualTo(10));
        }

        class CountedObject : IDisposable
        {
            public int Id { get; private set; }
            public static int[] InstanceCount { get; } = new int[16];

            public CountedObject(int id)
            {
                Id = id;
                InstanceCount[id]++;
            }

            public void Dispose()
            {
                if (Id < 0)
                {
                    throw new InvalidOperationException("Already disposed");
                }

                InstanceCount[Id]--;
                Id = -1;
            }
        }

        [Test]
        public void TestSharedReferences()
        {
            SharedReferenceManager<int, CountedObject> sharedReferenceManager = new(i => new CountedObject(i));

            Assert.That(CountedObject.InstanceCount, Is.All.Zero);

            using (var ref1 = sharedReferenceManager.GetReference(8))
            using (var ref2 = sharedReferenceManager.GetReference(11))
            {
                // New instances are instantiated in this scope
                var obj1 = (CountedObject)ref1;
                var obj2 = (CountedObject)ref2;
                Assert.That(obj1.Id, Is.EqualTo(8));
                Assert.That(obj2.Id, Is.EqualTo(11));
                Assert.AreNotEqual(obj2, obj1);
                Assert.That(CountedObject.InstanceCount[8], Is.EqualTo(1));
                Assert.That(CountedObject.InstanceCount[11], Is.EqualTo(1));
                using (var ref1_2 = sharedReferenceManager.GetReference(8))
                {
                    // Instance counts should never go above 1 (no new instances instantiated)
                    Assert.AreEqual(ref1_2.Value, obj1);
                    Assert.That(CountedObject.InstanceCount[8], Is.EqualTo(1));
                    Assert.That(CountedObject.InstanceCount[11], Is.EqualTo(1));
                }
            }

            // All instances disposed
            Assert.That(CountedObject.InstanceCount, Is.All.Zero);
        }
    }
}
