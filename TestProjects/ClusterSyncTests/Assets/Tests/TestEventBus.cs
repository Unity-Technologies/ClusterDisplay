using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.ClusterDisplay.Scripting;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.ClusterDisplay.Tests
{
    public class TestEventBus
    {
        EventBus<TestData> m_EventBus;
        // TransformSource m_TransformSource;
        // TransformReceiver m_TransformReceiver;

        [SetUp]
        public void SetUp()
        {
            m_EventBus = new EventBus<TestData>();
        }

        [Test]
        public void TestPublishAndSubscribe()
        {
            // simulates raw data being passed over the network
            NativeArray<byte> networkData = new(1024, Allocator.Temp);

            m_EventBus.Publish(new TestData
            {
                EnumVal = StateID.CustomEvents,
                LongVal = 0,
                FloatVal = 0.42f,
                Message = "Hello"
            });

            m_EventBus.Publish(new TestData
            {
                EnumVal = StateID.Time,
                LongVal = 1,
                FloatVal = 0.42f,
                Message = "Hello"
            });

            var callCount = 0;
            using var subscriber = m_EventBus.Subscribe(data =>
            {
                Assert.That(data.LongVal, Is.EqualTo(callCount));
                Assert.That(data.Message, Is.EqualTo("Hello"));
                Assert.That(data.FloatVal, Is.EqualTo(0.42f));
                callCount++;
            });

            var bulkCallCount = 0;
            using var bulkSub = m_EventBus.Subscribe(data =>
            {
                Assert.That(data.Length, Is.EqualTo(2));
                Assert.That(data[0].Message, Is.EqualTo("Hello"));
                Assert.That(data[1].Message, Is.EqualTo("Hello"));
                Assert.That(data[0].EnumVal, Is.EqualTo(StateID.CustomEvents));
                Assert.That(data[1].EnumVal, Is.EqualTo(StateID.Time));
                Assert.That(data[0].LongVal, Is.EqualTo(0));
                Assert.That(data[1].LongVal, Is.EqualTo(1));
                bulkCallCount++;
            });

            int dataSize = m_EventBus.SerializeAndFlush(networkData);
            m_EventBus.DeserializeAndPublish(networkData.GetSubArray(0, dataSize));

            Assert.That(callCount, Is.EqualTo(2));
            Assert.That(bulkCallCount, Is.EqualTo(1));
        }

        class TransformComparer : IEqualityComparer<Transform>
        {
            public bool Equals(Transform x, Transform y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.localPosition.Equals(y.localPosition) && x.localRotation.Equals(y.localRotation) && x.localScale.Equals(y.localScale);
            }

            public int GetHashCode(Transform obj)
            {
                return HashCode.Combine(obj.localPosition, obj.localRotation, obj.localScale);
            }
        }

#if false
        [UnityTest]
        public IEnumerator TestTransformSync()
        {
            // simulates raw data being passed over the network
            using NativeArray<byte> networkData = new(1024, Allocator.Persistent);


            m_TransformSource = new GameObject("Source", typeof(TransformSource))
                .GetComponent<TransformSource>();

            m_TransformReceiver = new GameObject("Receiver", typeof(TransformReceiver))
                .GetComponent<TransformReceiver>();

            // Inject a custom event bus into the scripts so we can simulate cluster network traffic
            using var eventBus = new EventBus<TransformMessage>();
            m_TransformSource.SetEventBus(eventBus);

            var receiverTransform = m_TransformReceiver.transform;
            receiverTransform.Translate(1, 1, 1);
            receiverTransform.Rotate(90, 60, 30);
            receiverTransform.localScale = new Vector3(2, 2.5f, 3);

            m_TransformReceiver.Source = m_TransformSource;

            var comparer = new TransformComparer();
            Assert.That(m_TransformSource.transform, Is.Not.EqualTo(receiverTransform).Using(comparer));

            yield return null;

            int dataSize = eventBus.SerializeAndFlush(networkData);
            eventBus.PublishLoopbackEvents();
            eventBus.DeserializeAndPublish(networkData.GetSubArray(0, dataSize));

            yield return null;
            Assert.That(m_TransformSource.transform, Is.EqualTo(receiverTransform).Using(comparer));

            var sourceTransform = m_TransformReceiver.transform;
            sourceTransform.Translate(3, 2, 1);
            sourceTransform.Rotate(5, 15, 25);
            sourceTransform.localScale = new Vector3(3, 2, 1);

            Assert.That(m_TransformSource.transform, Is.Not.EqualTo(receiverTransform).Using(comparer));

            yield return null;

            dataSize = eventBus.SerializeAndFlush(networkData);
            eventBus.PublishLoopbackEvents();
            eventBus.DeserializeAndPublish(networkData.GetSubArray(0, dataSize));

            yield return null;
            Assert.That(m_TransformSource.transform, Is.EqualTo(receiverTransform).Using(comparer));
        }
#endif
        [TearDown]
        public void TearDown()
        {
            m_EventBus.Dispose();

            // if (m_TransformReceiver)
            // {
            //     Object.Destroy(m_TransformReceiver.gameObject);
            // }
            //
            // if (m_TransformSource)
            // {
            //     Object.Destroy(m_TransformSource.gameObject);
            // }
        }
    }
}
