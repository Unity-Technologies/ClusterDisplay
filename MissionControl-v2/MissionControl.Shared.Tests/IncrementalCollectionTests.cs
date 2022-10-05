using System;
using System.Reflection;
using System.Collections;

namespace Unity.ClusterDisplay.MissionControl.Tests
{
    static class IncrementalCollectionExtensions
    {
        public static IncrementalCollectionTests.TestObject AddNewObject(
            this IncrementalCollection<IncrementalCollectionTests.TestObject> collection)
        {
            IncrementalCollectionTests.TestObject ret = new (Guid.NewGuid());
            collection.Add(ret);
            return ret;
        }

        public static IncrementalCollectionTests.TestObject AddNewObject(
            this IncrementalCollection<IncrementalCollectionTests.TestObject> collection, Guid id)
        {
            IncrementalCollectionTests.TestObject ret = new (id);
            collection.Add(ret);
            return ret;
        }
    }

    public class IncrementalCollectionTests
    {
        public class TestObject: IncrementalCollectionObject
        {
            public TestObject(Guid id): base(id)
            {
                var propInfo = GetType().GetProperty("FirstVersionNumber", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(propInfo, Is.Not.Null);
                m_FirstVersionNumber = propInfo!;

                propInfo = GetType().GetProperty("VersionNumber", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(propInfo, Is.Not.Null);
                m_VersionNumber = propInfo!;
            }

            public int Property { get; set; }

            public override bool Equals(object? obj)
            {
                if (!base.Equals(obj))
                {
                    return false;
                }
                var compareTo = (TestObject)obj;
                return Property == compareTo.Property;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override IncrementalCollectionObject NewOfTypeWithId()
            {
                return new TestObject(Id);
            }

            protected override void DeepCopyImp(IncrementalCollectionObject fromAbstract)
            {
                var from = (TestObject)fromAbstract;
                Property = from.Property;
            }

            public ulong FirstVersionNumberAccess => (ulong)m_FirstVersionNumber.GetValue(this)!;
            public ulong VersionNumberAccess => (ulong)m_VersionNumber.GetValue(this)!;

            PropertyInfo m_FirstVersionNumber;
            PropertyInfo m_VersionNumber;
        }

        class EventsMonitor
        {
            public EventsMonitor(IncrementalCollection<TestObject> collection)
            {
                collection.OnObjectAdded += o => Added.Add(o);
                collection.OnObjectRemoved += o => Removed.Add(o);
                collection.OnObjectUpdated += o => Updated.Add(o);
            }

            // ReSharper disable once MemberCanBePrivate.Local -> To be symmetric with Removed
            public List<TestObject> Added { get; } = new();
            public List<TestObject> Removed { get; } = new();
            // ReSharper disable once MemberCanBePrivate.Local -> To be symmetric with Removed
            public List<TestObject> Updated { get; } = new();

            public void CheckReferences(IEnumerable<TestObject> added, IEnumerable<TestObject> removed, IEnumerable<TestObject> updated)
            {
                Assert.That(Added.SequenceEqual(added, ObjectReferenceEqualityComparer<TestObject>.Default), Is.True);
                Assert.That(Removed.SequenceEqual(removed, ObjectReferenceEqualityComparer<TestObject>.Default), Is.True);
                Assert.That(Updated.SequenceEqual(updated, ObjectReferenceEqualityComparer<TestObject>.Default), Is.True);

                Added.Clear();
                Removed.Clear();
                Updated.Clear();
            }
        }

        [Test]
        public void Add()
        {
            IncrementalCollection<TestObject> testCollection = new();
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            // Simple add
            TestObject object1A = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1A);
            ulong initialFirstVersionNumber = object1A.FirstVersionNumberAccess;
            eventsMonitor.CheckReferences(new[]{ object1A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(1));

            // Try to replace through add, should throw.
            TestObject object1B = new(object1A.Id) { Property = 28 };
            Assert.Throws<ArgumentException>(() => testCollection.Add(object1B));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(1));

            // Change something on the object, should trigger an update on the array
            object1A.Property *= 2;
            object1A.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1A });
            Assert.That(testCollection.VersionNumber, Is.EqualTo(2));

            // Remove it from the collection (to test interaction with IncrementalCollectionRemovedMarker)
            Assert.That(testCollection.Remove(object1A.Id), Is.True);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object1A }, Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));
            // And add back
            testCollection.Add(object1A);
            Assert.That(object1A.FirstVersionNumberAccess, Is.EqualTo(initialFirstVersionNumber));
            eventsMonitor.CheckReferences(new[]{ object1A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(4));
        }

        [Test]
        public void Item()
        {
            IncrementalCollection<TestObject> testCollection = new();
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            // Simple add
            TestObject object1A = new(Guid.NewGuid()) { Property = 42 };
            testCollection[object1A.Id] = object1A;
            ulong initialFirstVersionNumber = object1A.FirstVersionNumberAccess;
            eventsMonitor.CheckReferences(new[]{ object1A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(1));

            // Replace
            TestObject object1B = new(object1A.Id) { Property = 28 };
            testCollection[object1A.Id] = object1B;
            Assert.That(object1B.FirstVersionNumberAccess, Is.EqualTo(initialFirstVersionNumber));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1B });
            Assert.That(testCollection.VersionNumber, Is.EqualTo(2));

            // Change something on the object, should trigger an update on the array
            object1B.Property *= 2;
            object1B.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1B });
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));

            // Verify that object1a callbacks are disconnected from the array by changing it.
            object1A.Property *= 2;
            object1A.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));

            // Check key vs id mismatch
            Assert.Throws<ArgumentException>(() => testCollection[Guid.NewGuid()] = object1B);
            Assert.Throws<ArgumentException>(() => testCollection[Guid.NewGuid()] = object1A);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));

            // Check object1b is still correctly connected to IncrementalCollection
            object1B.Property *= 2;
            object1B.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1B });
            Assert.That(testCollection.VersionNumber, Is.EqualTo(4));

            // Remove it from the collection (to test interaction with IncrementalCollectionRemovedMarker)
            Assert.That(testCollection.Remove(object1B.Id), Is.True);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object1B }, Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(5));
            // And add back (still through the indexer).
            testCollection[object1B.Id] = object1B;
            Assert.That(object1B.FirstVersionNumberAccess, Is.EqualTo(initialFirstVersionNumber));
            eventsMonitor.CheckReferences(new[]{ object1B }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(6));
        }

        [Test]
        public void AddKeyValue()
        {
            IncrementalCollection<TestObject> testCollection = new();
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            // Simple add
            TestObject object1A = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1A.Id, object1A);
            ulong initialFirstVersionNumber = object1A.FirstVersionNumberAccess;
            eventsMonitor.CheckReferences(new[]{ object1A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(1));

            // Try to replace through add, should throw.
            TestObject object1B = new(object1A.Id) { Property = 28 };
            Assert.Throws<ArgumentException>(() => testCollection.Add(object1B.Id, object1B));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(1));

            // Change something on the object, should trigger an update on the array
            object1A.Property *= 2;
            object1A.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1A });
            Assert.That(testCollection.VersionNumber, Is.EqualTo(2));

            // Check key vs id mismatch
            TestObject object2 = new(Guid.NewGuid()) { Property = 1234 };
            Assert.Throws<ArgumentException>(() => testCollection.Add(Guid.NewGuid(), object2));
            Assert.That(testCollection.VersionNumber, Is.EqualTo(2));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.DoesNotThrow(() => testCollection.Add(object2.Id, object2));
            eventsMonitor.CheckReferences(new[]{ object2 }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));

            // Remove it from the collection (to test interaction with IncrementalCollectionRemovedMarker)
            Assert.That(testCollection.Remove(object1A.Id), Is.True);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object1A }, Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(4));
            // And add back
            testCollection.Add(object1A.Id, object1A);
            Assert.That(object1A.FirstVersionNumberAccess, Is.EqualTo(initialFirstVersionNumber));
            eventsMonitor.CheckReferences(new[]{ object1A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(5));
        }

        [Test]
        public void Clear()
        {
            IncrementalCollection<TestObject> testCollection = new();
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            ulong object1InitialFirstVersionNumber = object1.FirstVersionNumberAccess;
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            ulong object2InitialFirstVersionNumber = object2.FirstVersionNumberAccess;
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };
            testCollection.Add(object3);
            ulong object3InitialFirstVersionNumber = object3.FirstVersionNumberAccess;
            eventsMonitor.CheckReferences(new[]{ object1, object2, object3 }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(3));

            // Remove one of the objects (to test it does not get removed twice)
            Assert.That(testCollection.Remove(object2.Id), Is.True);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object2 }, Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(4));

            testCollection.Clear();
            Assert.That(testCollection.Count, Is.EqualTo(0));
            // Remark: We have to manually check elements in eventsMonitor.Removed because there is no guarantee on
            // the order of the objects.
            Assert.That(eventsMonitor.Removed.Count, Is.EqualTo(2));
            Assert.That(eventsMonitor.Removed.Exists(o => ReferenceEquals(o, object1)), Is.True);
            Assert.That(eventsMonitor.Removed.Exists(o => ReferenceEquals(o, object3)), Is.True);
            eventsMonitor.Removed.Clear();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(5));

            // We should be able to add back everything
            testCollection.Add(object1);
            testCollection.Add(object2);
            testCollection.Add(object3);
            eventsMonitor.CheckReferences(new[]{ object1, object2, object3 }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(8));
            Assert.That(object1.FirstVersionNumberAccess, Is.EqualTo(object1InitialFirstVersionNumber));
            Assert.That(object1.VersionNumberAccess, Is.EqualTo(6));
            Assert.That(object2.FirstVersionNumberAccess, Is.EqualTo(object2InitialFirstVersionNumber));
            Assert.That(object2.VersionNumberAccess, Is.EqualTo(7));
            Assert.That(object3.FirstVersionNumberAccess, Is.EqualTo(object3InitialFirstVersionNumber));
            Assert.That(object3.VersionNumberAccess, Is.EqualTo(8));
        }

        [Test]
        public void ContainsKey()
        {
            IncrementalCollection<TestObject> testCollection = new();

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };

            Assert.That(testCollection.ContainsKey(object1.Id), Is.True);
            Assert.That(testCollection.ContainsKey(object2.Id), Is.True);
            Assert.That(testCollection.ContainsKey(object3.Id), Is.False);

            testCollection.Remove(object2.Id);

            Assert.That(testCollection.ContainsKey(object1.Id), Is.True);
            Assert.That(testCollection.ContainsKey(object2.Id), Is.False);
            Assert.That(testCollection.ContainsKey(object3.Id), Is.False);

            testCollection.Remove(object1.Id);

            Assert.That(testCollection.ContainsKey(object1.Id), Is.False);
            Assert.That(testCollection.ContainsKey(object2.Id), Is.False);
            Assert.That(testCollection.ContainsKey(object3.Id), Is.False);
        }

        [Test]
        public void ForEach()
        {
            IncrementalCollection<TestObject> testCollection = new();

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };
            testCollection.Add(object3);

            var keys = testCollection.Keys.ToHashSet();
            Assert.That(keys.Count, Is.EqualTo(3));

            foreach (var pair in testCollection)
            {
                Assert.That(pair.Key, Is.EqualTo(pair.Value.Id));
                Assert.That(keys.Remove(pair.Key), Is.True);
            }
        }

        [Test]
        public void Remove()
        {
            IncrementalCollection<TestObject> testCollection = new();
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };
            eventsMonitor.CheckReferences(new[]{ object1, object2 }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.Count, Is.EqualTo(2));

            // Try to remove something that is not in the collection
            Assert.That(testCollection.Remove(object3.Id), Is.False);
            Assert.That(testCollection.Count, Is.EqualTo(2));

            // Changing an object of the collection should affect the collection
            object1.Property *= 2;
            object1.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), new[]{ object1 });

            // Remove it object from the collection
            Assert.That(testCollection.Remove(object1.Id), Is.True);
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object1 }, Array.Empty<TestObject>());
            Assert.That(testCollection.Count, Is.EqualTo(1));

            // Changing the removed object shouldn't affect the collection anymore
            var versionNumberBefore = testCollection.VersionNumber;
            object1.Property *= 2;
            object1.SignalChanges();
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.VersionNumber, Is.EqualTo(versionNumberBefore));

            // Try to double remove
            Assert.That(testCollection.Remove(object1.Id), Is.False);
            Assert.That(testCollection.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGetValue()
        {
            IncrementalCollection<TestObject> testCollection = new();

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };

            Assert.That(testCollection.TryGetValue(object1.Id, out var gotObject), Is.True);
            Assert.That(gotObject, Is.SameAs(object1));
            Assert.That(testCollection.TryGetValue(object2.Id, out gotObject), Is.True);
            Assert.That(gotObject, Is.SameAs(object2));
            Assert.That(testCollection.TryGetValue(object3.Id, out gotObject), Is.False);
            Assert.That(gotObject, Is.Null);

            testCollection.Remove(object2.Id);

            Assert.That(testCollection.TryGetValue(object1.Id, out gotObject), Is.True);
            Assert.That(gotObject, Is.SameAs(object1));
            Assert.That(testCollection.TryGetValue(object2.Id, out gotObject), Is.False);
            Assert.That(gotObject, Is.Null);
            Assert.That(testCollection.TryGetValue(object3.Id, out gotObject), Is.False);
            Assert.That(gotObject, Is.Null);
        }

        [Test]
        public void DeltaWithAdd()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();

            // Get delta (with just 1)
            var update = collectionSrc.GetDeltaSince(0);
            Assert.That(update.UpdatedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.RemovedObjects, Is.Empty);
            Assert.That(collectionSrc.VersionNumber + 1, Is.EqualTo(update.NextUpdate));

            // Apply delta
            Assert.That(collectionDst, Is.Empty);
            collectionDst.ApplyDelta(update);
            Assert.That(collectionDst.Count(), Is.EqualTo(1));
            var objectFromDst = collectionDst[newObject1.Id];
            List<TestObject> newObjects = new();
            newObjects.Add(objectFromDst);
            Assert.That(objectFromDst, Is.EqualTo(newObject1));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst, Is.Not.SameAs(newObject1));

            collectionDstEventsMonitor.CheckReferences(newObjects, Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Add 2 new objects
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            var newObject3 = collectionSrc.AddNewObject();
            newObject3.Property = 4242442;

            // Get delta (with 2 objects)
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects.Count(), Is.EqualTo(2) );
            Assert.That(update.RemovedObjects, Is.Empty);
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            Assert.That(collectionDst.Count, Is.EqualTo(1));
            collectionDst.ApplyDelta(update);
            Assert.That(collectionDst.Count, Is.EqualTo(3));

            // Object from before changes should still be the same
            objectFromDst = collectionDst[newObject1.Id];
            Assert.That(objectFromDst, Is.EqualTo(newObject1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(1));

            // Test new objects
            newObjects.Clear();
            objectFromDst = collectionDst[newObject2.Id];
            newObjects.Add(objectFromDst);
            Assert.That(objectFromDst, Is.EqualTo(newObject2));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(2));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(2));
            Assert.That(objectFromDst, Is.Not.SameAs(newObject2));
            objectFromDst = collectionDst[newObject3.Id];
            newObjects.Add(objectFromDst);
            Assert.That(objectFromDst, Is.EqualTo(newObject3));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(2));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(2));
            Assert.That(objectFromDst, Is.Not.SameAs(newObject3));

            collectionDstEventsMonitor.CheckReferences(newObjects, Array.Empty<TestObject>(), Array.Empty<TestObject>());
        }

        [Test]
        public void DeltaWithChange()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject2.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Change object 1
            newObject1.Property = 28;
            newObject1.SignalChanges();

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.RemovedObjects, Is.Empty);
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            var object1FromDstBeforeChange = collectionDst[newObject1.Id];
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(),
                new[] { collectionDst[newObject1.Id] });

            // Test objects
            var objectFromDst = collectionDst[newObject1.Id];
            Assert.That(objectFromDst, Is.EqualTo(newObject1));
            Assert.That(objectFromDst, Is.SameAs(object1FromDstBeforeChange));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(2));
            objectFromDst = collectionDst[newObject2.Id];
            Assert.True( objectFromDst.Equals( newObject2 ) );
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(1));
        }

        [Test]
        public void DeltaWithManyChangeOnSameObject()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject1.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Change object 1 (twice)
            newObject1.Property = 28;
            newObject1.SignalChanges();

            newObject1.Property = 2828;
            newObject1.SignalChanges();

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.RemovedObjects, Is.Empty);
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            collectionDst.ApplyDelta(update );
            collectionDstEventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(),
                new[] { collectionDst[newObject1.Id] });

            // Test objects
            var objectFromDst = collectionDst[newObject1.Id];
            Assert.That(objectFromDst, Is.EqualTo(newObject1));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(2));
            objectFromDst = collectionDst[newObject2.Id];
            Assert.That(objectFromDst, Is.EqualTo(newObject2));
            Assert.That(objectFromDst.FirstVersionNumberAccess, Is.EqualTo(1));
            Assert.That(objectFromDst.VersionNumberAccess, Is.EqualTo(1));
        }

        [Test]
        public void DeltaWithRemove()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject2.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Remove object 1
            collectionSrc.Remove(newObject1.Id);

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects, Is.Empty);
            Assert.That(update.RemovedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            var oldObject1 = collectionDst[newObject1.Id];
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[] { oldObject1 },
                Array.Empty<TestObject>());

            // Test objects
            Assert.That(collectionDst.ContainsKey(newObject1.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject2.Id), Is.True);
        }

        [Test]
        public void DeltaSkipChangeIfRemoved()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject2.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Change object 1
            newObject1.Property = 28;
            newObject1.SignalChanges();

            // Remove object 1
            collectionSrc.Remove(newObject1.Id);

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects, Is.Empty);
            Assert.That(update.RemovedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            var oldObject1 = collectionDst[newObject1.Id];
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[] { oldObject1 },
                Array.Empty<TestObject>());

            // Test objects
            Assert.That(collectionDst.ContainsKey(newObject1.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject2.Id), Is.True);
        }

        [Test]
        public void DeltaSkipAddIfRemoved()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject2.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Add object 3 and 4
            var newObject3 = collectionSrc.AddNewObject();
            newObject3.Property = 424242;
            newObject3.SignalChanges();

            var newObject4 = collectionSrc.AddNewObject();
            newObject4.Property = 42424242;
            newObject4.SignalChanges();

            // Remove object 3
            collectionSrc.Remove(newObject3.Id);

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects.Count, Is.EqualTo(1));
            Assert.That(update.RemovedObjects, Is.Empty);
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject4.Id] }, Array.Empty<TestObject>(),
                Array.Empty<TestObject>());

            // Test objects
            Assert.That(collectionDst.ContainsKey(newObject1.Id), Is.True);
            Assert.That(collectionDst.ContainsKey(newObject2.Id), Is.True);
            Assert.That(collectionDst.ContainsKey(newObject3.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject4.Id), Is.True);
        }

        [Test]
        public void DeltaWithAddBackAfterRemove()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst1 = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst1);

            // Add objects to src and update dst from it.
            var newObject1 = collectionSrc.AddNewObject();
            var newObject2 = collectionSrc.AddNewObject();
            newObject1.Property = 28;
            newObject2.Property = 42;
            newObject1.SignalChanges();
            newObject2.SignalChanges();

            var update = collectionSrc.GetDeltaSince(0);
            collectionDst1.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst1[newObject1.Id], collectionDst1[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            Assert.That(collectionDst1.Count, Is.EqualTo(collectionSrc.Count));
            Assert.That(collectionDst1.TryGetValue(newObject1.Id, out var gotObject), Is.True);
            Assert.That(gotObject!.Property, Is.EqualTo(28));
            Assert.That(collectionDst1.TryGetValue(newObject2.Id, out gotObject), Is.True);
            Assert.That(gotObject!.Property, Is.EqualTo(42));

            // Remove newObject2 from src and update dst from this change
            bool ret = collectionSrc.Remove(newObject2.Id);
            Assert.That(ret, Is.True);

            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            var beforeRemove = collectionDst1[newObject2.Id];
            collectionDst1.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[] { beforeRemove },
                Array.Empty<TestObject>());

            Assert.That(collectionDst1.Count, Is.EqualTo(collectionSrc.Count));
            Assert.That(collectionDst1.TryGetValue(newObject1.Id, out gotObject), Is.True);
            Assert.That(gotObject!.Property, Is.EqualTo(28) );

            // Add a new object with the same id as newObject2
            var newObject2Take2 = collectionSrc.AddNewObject(newObject2.Id);
            newObject2Take2.Property = 4242;
            newObject2Take2.SignalChanges();
            Assert.That(collectionSrc.Count, Is.EqualTo(2));

            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            collectionDst1.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst1[newObject2.Id] }, Array.Empty<TestObject>(),
                Array.Empty<TestObject>());

            Assert.That(collectionDst1.Count, Is.EqualTo(collectionSrc.Count));
            Assert.That(collectionDst1.TryGetValue(newObject1.Id, out gotObject));
            Assert.That(gotObject!.Property, Is.EqualTo(28));
            Assert.That(collectionDst1.TryGetValue(newObject2.Id, out gotObject));
            Assert.That(gotObject!.Property, Is.EqualTo(4242));

            // Get all the changes from the start and mirror them to a new collection
            IncrementalCollection<TestObject> collectionDst2 = new();
            update = collectionSrc.GetDeltaSince(0);
            collectionDst2.ApplyDelta(update);
            Assert.That(collectionDst2.Count, Is.EqualTo(collectionSrc.Count));
            Assert.That(collectionDst2.TryGetValue(newObject1.Id, out gotObject));
            Assert.That(gotObject!.Property, Is.EqualTo(28));
            Assert.That(collectionDst2.TryGetValue(newObject2.Id, out gotObject));
            Assert.That(gotObject!.Property, Is.EqualTo(4242));
        }

        [Test]
        public void DeltaWithClear()
        {
            IncrementalCollection<TestObject> collectionSrc = new();
            IncrementalCollection<TestObject> collectionDst = new();
            EventsMonitor collectionDstEventsMonitor = new EventsMonitor(collectionDst);

            var newObject1 = collectionSrc.AddNewObject();
            newObject1.Property = 42;
            newObject1.SignalChanges();
            var newObject2 = collectionSrc.AddNewObject();
            newObject2.Property = 4242;
            newObject2.SignalChanges();

            // Get delta
            var update = collectionSrc.GetDeltaSince(0);

            // Apply delta
            collectionDst.ApplyDelta(update);
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject1.Id], collectionDst[newObject2.Id] },
                Array.Empty<TestObject>(), Array.Empty<TestObject>());

            // Add another object
            var newObject3 = collectionSrc.AddNewObject();
            newObject3.Property = 424242;
            newObject3.SignalChanges();

            // Clear everything
            collectionSrc.Clear();

            // And add a fourth object
            var newObject4 = collectionSrc.AddNewObject();
            newObject4.Property = 42424242;
            newObject4.SignalChanges();

            // Get delta
            update = collectionSrc.GetDeltaSince(update.NextUpdate);
            Assert.That(update.UpdatedObjects.Count(), Is.EqualTo(1));
            Assert.That(update.RemovedObjects.Count(), Is.EqualTo(2));
            Assert.That(update.NextUpdate, Is.EqualTo(collectionSrc.VersionNumber + 1));

            // Apply delta
            collectionDst.ApplyDelta(update);
            // Remark: We have to manually check elements in eventsMonitor.Removed because there is no guarantee on
            // the order of the objects.
            Assert.That(collectionDstEventsMonitor.Removed.Count, Is.EqualTo(2));
            Assert.That(collectionDstEventsMonitor.Removed.Exists(o => o.Id == newObject1.Id), Is.True);
            Assert.That(collectionDstEventsMonitor.Removed.Exists(o => o.Id == newObject2.Id), Is.True);
            collectionDstEventsMonitor.Removed.Clear();
            collectionDstEventsMonitor.CheckReferences(new[] { collectionDst[newObject4.Id] }, Array.Empty<TestObject>(),
                Array.Empty<TestObject>());

            // Test objects
            Assert.That(collectionDst.ContainsKey(newObject1.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject2.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject3.Id), Is.False);
            Assert.That(collectionDst.ContainsKey(newObject4.Id), Is.True);
        }

        [Test]
        public void CollectionKvpContains()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var collectionI = (ICollection<KeyValuePair<Guid, TestObject>>)testCollection;

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2A = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2A);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };

            TestObject object2B = new(object2A.Id) { Property = object2A.Property };

            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object1)), Is.True);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object2A)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object2B)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object3)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2A)), Is.True);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2B)), Is.True);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object3.Id, object3)), Is.False);

            testCollection.Remove(object2A.Id);

            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object1)), Is.True);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2A)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2B)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object3.Id, object3)), Is.False);

            testCollection.Remove(object1.Id);

            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object1.Id, object1)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2A)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object2A.Id, object2B)), Is.False);
            Assert.That(collectionI.Contains(new KeyValuePair<Guid, TestObject>(object3.Id, object3)), Is.False);
        }

        [Test]
        public void CollectionKvpRemove()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var collectionI = (ICollection<KeyValuePair<Guid, TestObject>>)testCollection;
            EventsMonitor eventsMonitor = new EventsMonitor(testCollection);

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2A = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2A);
            TestObject object2B = new(object2A.Id) { Property = 1234 };
            eventsMonitor.CheckReferences(new[]{ object1, object2A }, Array.Empty<TestObject>(), Array.Empty<TestObject>());
            Assert.That(testCollection.Count, Is.EqualTo(2));

            // Remove a pair
            Assert.That(collectionI.Remove(new KeyValuePair<Guid, TestObject>(object1.Id, object1)), Is.True);
            Assert.That(testCollection.Count, Is.EqualTo(1));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), new[]{ object1 }, Array.Empty<TestObject>());

            // Remove a pair but with the wrong value (shouldn't work)
            Assert.That(collectionI.Remove(new KeyValuePair<Guid, TestObject>(object2A.Id, object2B)), Is.False);
            Assert.That(testCollection.Count, Is.EqualTo(1));
            eventsMonitor.CheckReferences(Array.Empty<TestObject>(), Array.Empty<TestObject>(), Array.Empty<TestObject>());
        }

        [Test]
        public void CollectionKvpCopyTo()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var collectionI = (ICollection<KeyValuePair<Guid, TestObject>>)testCollection;

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };
            testCollection.Add(object3);

            var keys = testCollection.Keys.ToHashSet();
            Assert.That(keys.Count, Is.EqualTo(3));

            var testObjects = new KeyValuePair<Guid, TestObject>[3];
            collectionI.CopyTo(testObjects, 0);
            foreach (var pair in testObjects)
            {
                Assert.That(pair.Key, Is.EqualTo(pair.Value.Id));
                Assert.That(keys.Remove(pair.Key), Is.True);
            }
        }

        [Test]
        public void CollectionCopyTo()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var collectionI = (ICollection)testCollection;

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            testCollection.Add(object2);
            TestObject object3 = new(Guid.NewGuid()) { Property = 1234 };
            testCollection.Add(object3);

            var keys = testCollection.Keys.ToHashSet();
            Assert.That(keys.Count, Is.EqualTo(3));

            var testObjects = new object[3];
            collectionI.CopyTo(testObjects, 0);
            foreach (var currentObject in testObjects)
            {
                var pair = (KeyValuePair<Guid, TestObject>)currentObject;
                Assert.That(pair.Key, Is.EqualTo(pair.Value.Id));
                Assert.That(keys.Remove(pair.Key), Is.True);
            }
        }

        [Test]
        public void DictionaryItem()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var dictionaryI = (IDictionary)testCollection;

            // We know IDictionary Item is based on the IDictionary<> Item, with some additional type testing.  So
            // focus on type testing and assume the rest is good.
            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            dictionaryI[object1.Id] = object1;
            TestObject object2A = new(Guid.NewGuid()) { Property = 28 };
            Assert.Throws<ArgumentException>(() => dictionaryI["Bad type"] = object2A);
            Assert.Throws<ArgumentNullException>(() => dictionaryI[object2A.Id] = null);
            Assert.Throws<ArgumentException>(() => dictionaryI[object2A.Id] = "Bad type");
            dictionaryI[object2A.Id] = object2A;
            TestObject object2B = new(object2A.Id) { Property = 56 };
            dictionaryI[object2A.Id] = object2B;

            Assert.That(testCollection.Count, Is.EqualTo(2));
            Assert.That(testCollection.ContainsKey(object1.Id), Is.True);
            Assert.That(testCollection.TryGetValue(object2A.Id, out var gotObject2), Is.True);
            Assert.That(gotObject2, Is.Not.Null);
            Assert.That(gotObject2!.Property, Is.EqualTo(object2B.Property));
        }

        [Test]
        public void DictionaryAdd()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var dictionaryI = (IDictionary)testCollection;

            // We know IDictionary Add is based on the IDictionary<> Add, with some additional type testing.  So
            // focus on type testing and assume the rest is good.
            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            dictionaryI.Add(object1.Id, object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            Assert.Throws<ArgumentException>(() => dictionaryI.Add("Bad type", object2));
            Assert.Throws<ArgumentNullException>(() => dictionaryI.Add(object2.Id, null));
            Assert.Throws<ArgumentException>(() => dictionaryI.Add(object2.Id, "Bad type"));
            dictionaryI.Add(object2.Id, object2);

            Assert.That(testCollection.Count, Is.EqualTo(2));
            Assert.That(testCollection.ContainsKey(object1.Id), Is.True);
            Assert.That(testCollection.ContainsKey(object2.Id), Is.True);
        }

        [Test]
        public void DictionaryContains()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var dictionaryI = (IDictionary)testCollection;

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };

            Assert.That(dictionaryI.Contains(object1.Id), Is.True);
            Assert.That(dictionaryI.Contains(object2.Id), Is.False);
            Assert.That(dictionaryI.Contains(object1.Id.ToString()), Is.False);
        }

        [Test]
        public void DictionaryRemove()
        {
            IncrementalCollection<TestObject> testCollection = new();
            var dictionaryI = (IDictionary)testCollection;

            TestObject object1 = new(Guid.NewGuid()) { Property = 42 };
            testCollection.Add(object1);
            TestObject object2 = new(Guid.NewGuid()) { Property = 28 };
            Assert.That(testCollection.Count, Is.EqualTo(1));

            // Try to remove something that is not in the collection
            dictionaryI.Remove(object2.Id);
            Assert.That(testCollection.Count, Is.EqualTo(1));

            // Try to remove something of the wrong type
            dictionaryI.Remove("Wrong type");
            Assert.That(testCollection.Count, Is.EqualTo(1));

            // Now remove something in the collection
            dictionaryI.Remove(object1.Id);
            Assert.That(testCollection.Count, Is.EqualTo(0));
        }
    }
}
