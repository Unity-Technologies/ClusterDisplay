using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Scripting;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Unity.ClusterDisplay.Tests
{
    public class TestReplicator
    {
        GameObject m_SourceObject;
        GameObject m_DestObject;
        EditorLinkConfig m_LinkConfig;
        List<Object> m_Destructibles;

        [SetUp]
        public void SetUp()
        {
            var parentObject = new GameObject("Parent1");
            m_SourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_SourceObject.name = "Source";
            m_SourceObject.transform.parent = parentObject.transform;
            m_DestObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_DestObject.name = "Dest";

            m_SourceObject.AddComponent<SomeComponent>();
            m_DestObject.AddComponent<SomeComponent>();

            // Config is required, but we don't need to depend on real networking
            // for the tests.
            m_LinkConfig = ScriptableObject.CreateInstance<EditorLinkConfig>();
            m_LinkConfig.Parse("127.0.0.1:40001");

            m_Destructibles = new List<Object> {parentObject, m_DestObject, m_LinkConfig};
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in m_Destructibles)
            {
                Object.Destroy(obj);
            }
        }

        static void RandomizeTransform(Transform t)
        {
            t.Translate(Random.insideUnitSphere * 5f);
            t.Rotate(Random.rotation.eulerAngles);
            t.localScale.Scale(Random.insideUnitSphere);
        }

        [Test]
        public void TestTransformReplicator()
        {
            var tempBuffer = new NativeArray<byte>(ushort.MaxValue, Allocator.Temp);
            var transformComparer = new TransformComparer();
            var messageComparerApprox = new TransformMessageComparer();
            var sourceTransform = m_SourceObject.transform;
            var destTransform = m_DestObject.transform;

            // Note about comparison tests:
            // We want to ensure that transform properties on source and dest and (bit-wise) identical
            // (i.e. floating point values are EXACTLY the same), so we should apply floating-point
            // equality when comparing 2 Transforms.
            // However, we cannot guarantee that floating point values are exactly preserved when undergoing
            // TransformMessage -> Transform component -> TransformMessage
            // so when comparing TransformMessage and Transform component, we need to use an approximate
            // equality.

            // ============= Initialization ===========
            var guid = Guid.NewGuid();
            var replicatorEmitter = new TransformReplicator(guid, sourceTransform);
            replicatorEmitter.Initialize(ReplicatorMode.Emitter);

            var replicatorRepeater = new TransformReplicator(guid, destTransform);
            replicatorRepeater.Initialize(ReplicatorMode.Repeater);

            // Also test with an editor link
            using var link = new EditorLink(m_LinkConfig, false);
            replicatorEmitter.EditorLink = link;
            var linkData = new byte[1024];

            // ============= During frame 0 ===========
            // t0 - new transform at frame 0 (set by arbitrary game logic)
            RandomizeTransform(sourceTransform);
            Assert.That(sourceTransform, Is.Not.EqualTo(destTransform).Using(transformComparer));
            var t0 = new TransformMessage(sourceTransform);

            // End of frame 0  - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replication value is t0
            // Check that published value is correct
            var publishedMessage = replicatorEmitter.EventBus.OutBuffer.LoadStruct<ReplicationMessage<TransformMessage>>();
            Assert.That(publishedMessage.Guid, Is.EqualTo(guid));
            Assert.That(publishedMessage.Contents, Is.EqualTo(t0)); // Note: exact comparison using default comparer
            replicatorEmitter.EventBus.SerializeAndFlush(tempBuffer);
            replicatorEmitter.EventBus.PublishLoopbackEvents();

            // Replications messages are propagated at the next sync point (beginning of the next frame), so
            // at the end of frame 0, emitter and repeater may actually be out of sync!

            // =========== Beginning of frame 1 - Sync point ============

            replicatorRepeater.OnPreFrame();
            replicatorEmitter.OnPreFrame();

            // Repeater reads replication data during the sync point
            // During the replicator update (end of frame), we'll check that the
            // data has been applied.
            replicatorRepeater.EventBus.DeserializeAndPublish(tempBuffer);

            // During frame 1
            // Let's try setting the transform on the emitter using an Editor Link this time.
            // t1 - new transform at frame 1 (set by editor link)
            var t1 = new TransformMessage(
                new Vector3(1, 2, 3),
                Quaternion.Euler(10, 20, 30).normalized,
                new Vector3(3, 2, 1));
            var msg = new ReplicationMessage<TransformMessage>(guid, t1);
            msg.StoreInBuffer(linkData);

            // This passes link data to the replicator
            link.EnqueueReceivedData(linkData);
            link.ProcessIncomingMessages();

            // End of frame 1 - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replicating the latest published value, both emitter and repeater objects
            // should be set to t0
            Assert.That(t0, Is.EqualTo(new TransformMessage(sourceTransform)).Using(messageComparerApprox));
            Assert.That(sourceTransform, Is.EqualTo(destTransform).Using(transformComparer));

            // Next replication value is t1
            publishedMessage = replicatorEmitter.EventBus.OutBuffer.LoadStruct<ReplicationMessage<TransformMessage>>();
            Assert.That(publishedMessage.Guid, Is.EqualTo(guid));
            Assert.That(publishedMessage.Contents, Is.EqualTo(t1));
            replicatorEmitter.EventBus.SerializeAndFlush(tempBuffer);
            replicatorEmitter.EventBus.PublishLoopbackEvents();

            // =========== Beginning of frame 2 - Sync point ============
            replicatorEmitter.OnPreFrame();
            replicatorRepeater.OnPreFrame();

            // Repeater reads replication data during the sync point
            replicatorRepeater.EventBus.DeserializeAndPublish(tempBuffer);

            // On the emitter, we see t1 when we start a new frame (even though we had just rendered t0
            // on frame 1
            Assert.That(t1, Is.EqualTo(new TransformMessage(sourceTransform)).Using(messageComparerApprox));

            // End of frame 1 - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replicating the latest published value, both emitter and repeater objects
            // should be set to t1
            Assert.That(t1, Is.EqualTo(new TransformMessage(sourceTransform)).Using(messageComparerApprox));
            Assert.That(sourceTransform, Is.EqualTo(destTransform).Using(transformComparer));
        }

        [Test]
        public void TestGenericReplicator()
        {
            var tempBuffer = new NativeArray<byte>(ushort.MaxValue, Allocator.Temp);
            var sourceComponent = m_SourceObject.GetComponent<SomeComponent>();
            var destComponent = m_DestObject.GetComponent<SomeComponent>();

            // ============= Initialization ===========
            var guid = Guid.NewGuid();

            var propertyInfo = typeof(SomeComponent).GetProperty("MyProperty");
            var replicatorEmitter =
                new GenericPropertyReplicatorImpl<float>(guid, new PropertyMember(sourceComponent, propertyInfo));
            replicatorEmitter.Initialize(ReplicatorMode.Emitter);

            var replicatorRepeater =
                new GenericPropertyReplicatorImpl<float>(guid, new PropertyMember(destComponent, propertyInfo));
            replicatorRepeater.Initialize(ReplicatorMode.Repeater);

            // Also test with an editor link
            using var link = new EditorLink(m_LinkConfig, false);
            replicatorEmitter.EditorLink = link;

            var linkData = new byte[1024];

            // ============= During frame 0 ===========
            // t0 - new value at frame 0 (set by arbitrary game logic)
            sourceComponent.MyProperty = Random.value;
            Assert.That(sourceComponent, Is.Not.EqualTo(destComponent));
            var t0 = sourceComponent.MyProperty;

            // End of frame 0  - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replication value is t0
            // Check that published value is correct
            var publishedMessage = replicatorEmitter.EventBus.OutBuffer.LoadStruct<ReplicationMessage<float>>();
            Assert.That(publishedMessage.Guid, Is.EqualTo(guid));
            Assert.That(publishedMessage.Contents, Is.EqualTo(t0));
            replicatorEmitter.EventBus.SerializeAndFlush(tempBuffer);
            replicatorEmitter.EventBus.PublishLoopbackEvents();

            // Replications messages are propagated at the next sync point (beginning of the next frame), so
            // at the end of frame 0, emitter and repeater may actually be out of sync!

            // =========== Beginning of frame 1 - Sync point ============

            replicatorRepeater.OnPreFrame();
            replicatorEmitter.OnPreFrame();

            // Repeater reads replication data during the sync point
            // During the replicator update (end of frame), we'll check that the
            // data has been applied.
            replicatorRepeater.EventBus.DeserializeAndPublish(tempBuffer);

            // During frame 1
            // Let's try setting the transform on the emitter using an Editor Link this time.
            // t1 - new transform at frame 1 (set by editor link)
            var t1 = Random.value;
            var msg = new ReplicationMessage<float>(guid, t1);
            msg.StoreInBuffer(linkData);

            // This passes link data to the replicator
            link.EnqueueReceivedData(linkData);
            link.ProcessIncomingMessages();

            // End of frame 1 - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replicating the latest published value, both emitter and repeater objects
            // should be set to t0
            Assert.That(t0, Is.EqualTo(sourceComponent.MyProperty));
            Assert.That(sourceComponent, Is.EqualTo(destComponent));

            // Next replication value is t1
            publishedMessage = replicatorEmitter.EventBus.OutBuffer.LoadStruct<ReplicationMessage<float>>();
            Assert.That(publishedMessage.Guid, Is.EqualTo(guid));
            Assert.That(publishedMessage.Contents, Is.EqualTo(t1));
            replicatorEmitter.EventBus.SerializeAndFlush(tempBuffer);
            replicatorEmitter.EventBus.PublishLoopbackEvents();

            // =========== Beginning of frame 2 - Sync point ============
            replicatorEmitter.OnPreFrame();
            replicatorRepeater.OnPreFrame();

            // Repeater reads replication data during the sync point
            replicatorRepeater.EventBus.DeserializeAndPublish(tempBuffer);

            // On the emitter, we see t1 when we start a new frame (even though we had just rendered t0
            // on frame 1
            Assert.That(t1, Is.EqualTo(sourceComponent.MyProperty));

            // End of frame 1 - do replicator updates
            replicatorEmitter.Update();
            replicatorRepeater.Update();

            // Replicating the latest published value, both emitter and repeater objects
            // should be set to t1
            Assert.That(t1, Is.EqualTo(sourceComponent.MyProperty));
            Assert.That(sourceComponent, Is.EqualTo(destComponent));
        }
    }
}
