using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using Unity.Collections;
using Utils;
using Random = System.Random;

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
            ServiceLocator.Withdraw<IFoo>();
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

            ServiceLocator.Withdraw<IFoo>();
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFoo>());
            Assert.IsFalse(ServiceLocator.TryGet<IFoo>(out _));
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

        [Test]
        public void TestNameBasedGUID()
        {
            var namespaceGuid1 = Guid.NewGuid();
            var namespaceGuid2 = Guid.NewGuid();
            const string name1 = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
            const string name2 = "Ut in semper nibh";
            var guid1 = GuidUtils.GetNameBasedGuid(namespaceGuid1, name1);
            var guid2 = GuidUtils.GetNameBasedGuid(namespaceGuid1, name2);
            var guid3 = GuidUtils.GetNameBasedGuid(namespaceGuid1, name1);
            var guid4 = GuidUtils.GetNameBasedGuid(namespaceGuid2, name1);

            // Same namespace, different names
            Assert.AreNotEqual(guid1, guid2);

            // Same namespace, same names
            Assert.AreEqual(guid1, guid3);

            // Different namespace, same names
            Assert.AreNotEqual(guid1, guid4);

            // 4 most sig. bits of time_hi_and_version (octets 6-7) should equal 5,
            // indicating name-based GUID that uses SHA-1
            // (i.e. higher 4 bits of octet 6)
            var bytes = guid1.ToByteArray();
            // In .NET byte ordering, octet 6 is actually byte 7
            Assert.That(bytes[7] & 0b0101_0000, Is.EqualTo(0b0101_0000));
        }

        [Test]
        public void NodeIdBitVectorDefaultConstructor()
        {
            TestHasBitsSet(new NodeIdBitVector(), Enumerable.Empty<byte>());
            TestHasBitsSet(new NodeIdBitVectorReadOnly(), Enumerable.Empty<byte>());
        }

        [Test]
        public void NodeIdBitVectorBytesConstructor()
        {
            TestHasBitsSet(new NodeIdBitVector(k_SomeBits), k_SomeBits);
            TestHasBitsSet(new NodeIdBitVectorReadOnly(k_SomeBits), k_SomeBits);
            TestHasBitsSet(new NodeIdBitVector(k_ManyBits), k_ManyBits);
            TestHasBitsSet(new NodeIdBitVectorReadOnly(k_ManyBits), k_ManyBits);
            TestHasBitsSet(new NodeIdBitVector(k_ManyBits.Concat(k_ManyBits)), k_ManyBits);
            TestHasBitsSet(new NodeIdBitVectorReadOnly(k_ManyBits.Concat(k_ManyBits)), k_ManyBits);
        }

        [Test]
        public void NodeIdBitVectorItemSet()
        {
            var toTest = new NodeIdBitVector();

            toTest[28] = true;
            toTest[42] = true;
            toTest[63] = true;
            toTest[64] = true;
            TestHasBitsSet(toTest, new byte[]{ 28, 42, 63, 64 });

            toTest[42] = false;
            toTest[63] = true;
            toTest[64] = true;
            toTest[142] = true;
            TestHasBitsSet(toTest, new byte[]{ 28, 63, 64, 142 });
        }

        [Test]
        public void NodeIdBitVectorCopyConstructor()
        {
            var original = new NodeIdBitVector(k_SomeBits);

            var test = new NodeIdBitVector(original);
            TestHasBitsSet(test, k_SomeBits);
            var testReadOnly = new NodeIdBitVectorReadOnly(original);
            TestHasBitsSet(testReadOnly, k_SomeBits);

            // Change the original (to be sure they are really independent)
            original[28] = false;
            original[56] = true;
            original[64] = true;

            TestHasBitsSet(test, k_SomeBits);
            TestHasBitsSet(testReadOnly, k_SomeBits);
        }

        [Test]
        public void NodeIdBitVectorFromIndividualULongConstructor()
        {
            TestHasBitsSet(new NodeIdBitVector(k_ManyBitsStorage[0], k_ManyBitsStorage[1], k_ManyBitsStorage[2],
                k_ManyBitsStorage[3]), k_ManyBits);
            TestHasBitsSet(new NodeIdBitVectorReadOnly(k_ManyBitsStorage[0], k_ManyBitsStorage[1], k_ManyBitsStorage[2],
                k_ManyBitsStorage[3]), k_ManyBits);
        }

        [Test]
        public void NodeIdBitVectorCopyToUlongArray()
        {
            var toTest = new NodeIdBitVectorReadOnly(k_ManyBits);
            var ulongArray = new ulong[4];
            toTest.CopyTo(ulongArray);
            Assert.That(ulongArray[0], Is.EqualTo(k_ManyBitsStorage[0]));
            Assert.That(ulongArray[1], Is.EqualTo(k_ManyBitsStorage[1]));
            Assert.That(ulongArray[2], Is.EqualTo(k_ManyBitsStorage[2]));
            Assert.That(ulongArray[3], Is.EqualTo(k_ManyBitsStorage[3]));
        }

        [Test]
        public void NodeIdBitVectorCopyToIndividualULong()
        {
            var toTest = new NodeIdBitVectorReadOnly(k_ManyBits);
            toTest.CopyTo(out var ulong0, out var ulong1, out var ulong2, out var ulong3);
            Assert.That(ulong0, Is.EqualTo(k_ManyBitsStorage[0]));
            Assert.That(ulong1, Is.EqualTo(k_ManyBitsStorage[1]));
            Assert.That(ulong2, Is.EqualTo(k_ManyBitsStorage[2]));
            Assert.That(ulong3, Is.EqualTo(k_ManyBitsStorage[3]));
        }

        [Test]
        public void NodeIdBitVectorCopyToBitField64Array()
        {
            var toTest = new NodeIdBitVectorReadOnly(k_ManyBits);
            var bitField64Array = new BitField64[4];
            toTest.CopyTo(bitField64Array);
            Assert.That(bitField64Array[0].Value, Is.EqualTo(k_ManyBitsStorage[0]));
            Assert.That(bitField64Array[1].Value, Is.EqualTo(k_ManyBitsStorage[1]));
            Assert.That(bitField64Array[2].Value, Is.EqualTo(k_ManyBitsStorage[2]));
            Assert.That(bitField64Array[3].Value, Is.EqualTo(k_ManyBitsStorage[3]));
        }

        [Test]
        public void NodeIdBitVectorExtractBitsSet()
        {
            Assert.That(new NodeIdBitVectorReadOnly(k_ManyBits).ExtractSetBits(), Is.EqualTo(k_ManyBits));
        }

        [Test]
        public void NodeIdBitVectorToString()
        {
            Assert.That(new NodeIdBitVectorReadOnly(k_SomeBits).ToString(), Is.EqualTo("2, 28, 42, 164, 211"));
        }

        [Test]
        public void NodeIdBitVectorSet()
        {
            var someBits = new NodeIdBitVectorReadOnly(k_SomeBits);
            var manyBits = new NodeIdBitVectorReadOnly(k_ManyBits);

            var toTest = new NodeIdBitVector();
            toTest.Set(someBits);
            TestHasBitsSet(toTest, k_SomeBits);
            toTest.Set(manyBits);
            TestHasBitsSet(toTest, k_SomeBits.Concat(k_ManyBits));
        }

        [Test]
        public void NodeIdBitVectorClear()
        {
            var toTest = new NodeIdBitVector(k_ManyBits);
            var randomizer = new Random(Guid.NewGuid().GetHashCode());
            var bitsToRemove = k_ManyBits.Where(_ => randomizer.Next(0, 2) == 1).ToArray();
            toTest.Clear(new NodeIdBitVectorReadOnly(bitsToRemove));
            TestHasBitsSet(toTest, k_ManyBits.Where(b => !bitsToRemove.Contains(b)));
        }

        static readonly byte[] k_SomeBits = { 2, 28, 42, 164, 211 };
        static readonly byte[] k_ManyBits = {
            3, 7, 11, 15, 19, 23, 27, 31, 35, 39, 43, 47, 51, 55, 59, 63,
            66, 70, 74, 78, 82, 86, 90, 94, 98, 102, 106, 110, 114, 118, 122, 126,
            129, 133, 137, 141, 145, 149, 153, 157, 161, 165, 169, 173, 177, 181, 185, 189,
            192, 196, 200, 204, 208, 212, 216, 220, 224, 228, 232, 236, 240, 244, 248, 252
        };
        static readonly ulong[] k_ManyBitsStorage = {0x8888888888888888, 0x4444444444444444, 0x2222222222222222,
            0x1111111111111111};

        void TestHasBitsSet(NodeIdBitVectorReadOnly nodeIdBitVector, IEnumerable<byte> trueIndices)
        {
            int exepectedSetBitCount = 0;
            for (int i = byte.MinValue; i < byte.MaxValue; ++i)
            {
                bool expectedValue = trueIndices.Contains((byte)i);
                if (expectedValue)
                {
                    ++exepectedSetBitCount;
                }
                Assert.That(nodeIdBitVector[(byte)i], Is.EqualTo(expectedValue));
            }
            Assert.That(nodeIdBitVector.SetBitsCount, Is.EqualTo(exepectedSetBitCount));
        }

        [Test]
        public void ArrayWithSpinLock()
        {
            var testArray = new ArrayWithSpinLock<int>();

            void SetArrayWithTwoElementsContent()
            {
                using var arrayLock = testArray.Lock();
                arrayLock.SetArray(new int[]{42,28});
            }

            Thread testThread;
            using (var arrayLock = testArray.Lock())
            {
                testThread = new Thread(SetArrayWithTwoElementsContent);
                testThread.Start();

                Thread.Sleep(10); // To be sure testThread has the time to block on the lock call

                Assert.That(arrayLock.GetArray(), Is.Empty);
                arrayLock.SetArray(new int[]{68});
            }
            // The unlock moving out of the using should allow testThread to proceed and replace the content of the
            // ArrayWithSpinLock.
            Assert.That(testThread.Join(TimeSpan.FromSeconds(15)), Is.True);

            using (var arrayLock = testArray.Lock())
            {
                Assert.That(arrayLock.GetArray().Length, Is.EqualTo(2));
                Assert.That(arrayLock.GetArray()[0], Is.EqualTo(42));
                Assert.That(arrayLock.GetArray()[1], Is.EqualTo(28));
            }
        }

        [Test]
        public void ArrayWithSpinLockAppendRemove()
        {
            var testArray = new ArrayWithSpinLock<int>();
            using var lockedArray = testArray.Lock();
            Assert.That(lockedArray.GetArray(), Is.Empty);
            lockedArray.AppendToArray(42);
            lockedArray.AppendToArray(28);
            lockedArray.AppendToArray(4228);
            lockedArray.AppendToArray(2842);
            lockedArray.AppendToArray(666);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{42,28,4228,2842,666}));
            lockedArray.RemoveFromArray(42);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{28,4228,2842,666}));
            lockedArray.RemoveFromArray(666);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{28,4228,2842}));
            lockedArray.RemoveFromArray(4228);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{28,2842}));
            lockedArray.RemoveFromArray(28);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{2842}));
            lockedArray.RemoveFromArray(2842);
            Assert.That(lockedArray.GetArray(), Is.EqualTo(new int[]{}));
        }
    }
}
