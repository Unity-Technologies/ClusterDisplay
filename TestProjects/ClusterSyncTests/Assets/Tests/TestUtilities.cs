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
    }
}
