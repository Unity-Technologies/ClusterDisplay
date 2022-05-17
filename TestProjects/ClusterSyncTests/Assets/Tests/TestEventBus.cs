using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    public class TestEventBus
    {
        EventBus<TestData> m_EventBus;

        [SetUp]
        public void SetUp()
        {
            m_EventBus = new EventBus<TestData>();
        }

        [Test]
        public void TestPublishAndSubscribe()
        {
            m_EventBus.Publish(new TestData
            {
                EnumVal = StateID.CustomData,
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
                Assert.That(data[0].EnumVal, Is.EqualTo(StateID.CustomData));
                Assert.That(data[1].EnumVal, Is.EqualTo(StateID.Time));
                Assert.That(data[0].LongVal, Is.EqualTo(0));
                Assert.That(data[1].LongVal, Is.EqualTo(1));
                bulkCallCount++;
            });

            m_EventBus.DeserializeAndPublish(m_EventBus.OutBuffer);

            Assert.That(callCount, Is.EqualTo(2));
            Assert.That(bulkCallCount, Is.EqualTo(1));
        }

        [TearDown]
        public void TearDown()
        {
            m_EventBus.Dispose();
        }
    }
}
