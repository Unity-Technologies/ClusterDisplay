using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using Object = UnityEngine.Object;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;
using static Unity.ClusterDisplay.Tests.Utilities;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncTests
    {
        GameObject m_TestGameObject;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;
        string m_InterfaceName;

        readonly List<ClusterSync> m_Instances = new();
        UdpAgent m_TestAgent;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Provide<IClusterSyncState>(null);
            m_InterfaceName = SelectNic().Name;
        }

        UdpAgent GetTestAgent(IEnumerable<MessageType> receiveMessageTypes)
        {
            var testConfig = udpConfig;
            testConfig.AdapterName = m_InterfaceName;
            testConfig.ReceivedMessagesType = receiveMessageTypes.ToArray();

            return new UdpAgent(testConfig);
        }

        [Test]
        public void TestClusterDisplayState()
        {
            Assert.That(ClusterDisplayState.GetIsActive(), Is.False);

            var clusterSync = new ClusterSync();
            ServiceLocator.Provide<IClusterSyncState>(clusterSync);

            Assert.That(ClusterDisplayState.GetIsClusterLogicEnabled(), Is.False);

            m_Instances.Add(clusterSync);
        }

        [UnityTest]
        public IEnumerator TestClusterSetupEmitter()
        {
            const int numRepeaters = 1;

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            emitterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                Role = NodeRole.Emitter,
                NodeID = k_EmitterId,
                RepeaterCount = numRepeaters,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                Port = TestPort,
                CommunicationTimeout = Timeout,
                HandshakeTimeout = Timeout
            });
            m_TestAgent = GetTestAgent(RepeaterNode.ReceiveMessageTypes);

            using var emitterEventBus = new EventBus<TestData>(emitterClusterSync);
            int receivedOnLoopback = 0;
            emitterEventBus.Subscribe(testData =>
            {
                ++receivedOnLoopback;

                switch (testData.EnumVal)
                {
                case StateID.Random:
                    Assert.That(testData.LongVal, Is.EqualTo(42));
                    Assert.That(testData.FloatVal, Is.EqualTo(28.42f));
                    break;
                case StateID.Input:
                    Assert.That(testData.LongVal, Is.EqualTo(28));
                    Assert.That(testData.FloatVal, Is.EqualTo(42.28f));
                    break;
                default:
                    Assert.Fail("Unexpected testData.EnumVal");
                    break;
                }
            });

            long testDeadline = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(10));

            var registerWithEmitterTask = Task.Run(() =>
            {
                // Register with emitter
                m_TestAgent.SendMessage(MessageType.RegisteringWithEmitter, new RegisteringWithEmitter
                {
                    NodeId = k_RepeaterId,
                    IPAddressBytes = BitConverter.ToUInt32(m_TestAgent.AdapterAddress.GetAddressBytes())
                });

                // Wait for answer
                Assert.That(ConsumeFirstMessageOfRepeaterWhere<RepeaterRegistered>(m_TestAgent, testDeadline,
                    message => message.Payload is {NodeId: k_RepeaterId, Accepted: true}), Is.True);

                bool receivedEmitterWaitingToStartFrame = false;
                while (Stopwatch.GetTimestamp() < testDeadline && !receivedEmitterWaitingToStartFrame)
                {
                    // Inform emitter that the repeater is ready to start its test
                    m_TestAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame, new RepeaterWaitingToStartFrame
                    {
                        FrameIndex = 0, NodeId = k_RepeaterId, WillUseNetworkSyncOnNextFrame = true
                    });

                    // Wait for answer (but not for too long as it is possible we sent previous
                    // RepeaterWaitingToStartFrame before the emitter was ready to deal with it...
                    receivedEmitterWaitingToStartFrame = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                        m_TestAgent, StopwatchUtils.TimestampIn(TimeSpan.FromMilliseconds(100)),
                        message => !message.Payload.IsWaitingOn(k_RepeaterId));
                }
                Assert.That(receivedEmitterWaitingToStartFrame, Is.True);
            });

            Assert.That(emitterClusterSync.NodeRole, Is.EqualTo(NodeRole.Emitter));
            Assert.That(emitterClusterSync.NodeID, Is.EqualTo(k_EmitterId));
            Assert.That(emitterClusterSync.IsClusterLogicEnabled, Is.True);

            var node = emitterClusterSync.LocalNode as EmitterNode;
            Assert.That(node, Is.Not.Null);
            Assert.That(node, Is.Not.Null);
            Assert.That(node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.Zero);
            Assert.That(node.EmitterConfig.ExpectedRepeaterCount, Is.EqualTo(numRepeaters));
            Assert.That(node.UsingNetworkSync, Is.True);

            emitterEventBus.Publish(new TestData
            {
                EnumVal = StateID.Random,
                LongVal = 42,
                FloatVal = 28.42f,
            });

            emitterEventBus.Publish(new TestData
            {
                EnumVal = StateID.Input,
                LongVal = 28,
                FloatVal = 42.28f,
            });

            while (node.RepeatersStatus.RepeaterPresence.SetBitsCount != numRepeaters)
            {
                yield return null;
            }

            Assert.DoesNotThrow(registerWithEmitterTask.Wait);
            Assert.That(receivedOnLoopback, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TestClusterSetupRepeater()
        {
            var repeaterClusterSync = new ClusterSync("Repeater");
            m_Instances.Add(repeaterClusterSync);

            repeaterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                Role = NodeRole.Repeater,
                NodeID = k_RepeaterId,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                Port = TestPort,
                CommunicationTimeout = Timeout,
                HandshakeTimeout = Timeout,
                Fence = FrameSyncFence.External
            });

            m_TestAgent = GetTestAgent(EmitterNode.ReceiveMessageTypes);

            Assert.That(repeaterClusterSync.IsClusterLogicEnabled, Is.True);

            Assert.That(repeaterClusterSync.NodeRole, Is.EqualTo(NodeRole.Repeater));
            Assert.That(repeaterClusterSync.NodeID, Is.EqualTo(k_RepeaterId));

            var node = repeaterClusterSync.LocalNode as RepeaterNode;
            Assert.That(node, Is.Not.Null);
            Assert.That(node.UsingNetworkSync, Is.False);

            long testDeadline = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(10));

            using var emitterFrameDataSplitter = new FrameDataSplitter(m_TestAgent);

            var emitFirstFrame = Task.Run(() =>
            {
                // Receive repeaters registration message
                using var receivedRegisteringMessage = m_TestAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testDeadline)) as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(receivedRegisteringMessage, Is.Not.Null);
                Assert.That(receivedRegisteringMessage.Payload.NodeId, Is.EqualTo(k_RepeaterId));

                // Answer
                m_TestAgent.SendMessage(MessageType.RepeaterRegistered, new RepeaterRegistered
                {
                    NodeId = receivedRegisteringMessage.Payload.NodeId,
                    IPAddressBytes = receivedRegisteringMessage.Payload.IPAddressBytes,
                    Accepted = true
                });

                // Receive message about ready to start frame
                Assert.That(ConsumeFirstMessageOfRepeaterWhere<RepeaterWaitingToStartFrame>(m_TestAgent, testDeadline,
                    message => message.Payload is {FrameIndex: 0, NodeId: k_RepeaterId}), Is.True);

                // Answer
                m_TestAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                    new EmitterWaitingToStartFrame {FrameIndex = 0});

                // Transmit the first frame's data
                // Remarks: Transmit "dummy data" as valid state data would cause confusion in the unit test runner
                // system and "yield return null" would not return "soon enough" and the test would timeout.
                // ReSharper disable once AccessToDisposedClosure
                var frameDataBuffer = emitterFrameDataSplitter.GetNewFrameDataBuffer();
                frameDataBuffer.Store(42, nativeArray =>
                {
                    nativeArray.CopyTo(AllocRandomByteArray(nativeArray.Length));
                    return nativeArray.Length;
                });
                // ReSharper disable once AccessToDisposedClosure
                emitterFrameDataSplitter.SendFrameData(0, ref frameDataBuffer);
            });

            while (node.FrameIndex == 0)
            {
                yield return null;
            }

            Assert.DoesNotThrow(emitFirstFrame.Wait);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_TestGameObject != null)
            {
                Object.Destroy(m_TestGameObject);
            }

            foreach (var instance in m_Instances)
            {
                instance.DisableClusterDisplay();
            }

            m_TestAgent?.Dispose();

            m_Instances.Clear();
        }

        static bool ConsumeFirstMessageOfRepeaterWhere<T>(IUdpAgent udpAgent, long testEndTimestamp,
            Func<ReceivedMessage<T>, bool> predicate, IEnumerable<MessageType> typesToSkip = null) where T: unmanaged
        {
            var typesToSkipHashSet = new HashSet<MessageType>();
            if (typesToSkip != null)
            {
                foreach (var type in typesToSkip)
                {
                    typesToSkipHashSet.Add(type);
                }
            }

            for (;;)
            {
                using var receivedMessage = udpAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testEndTimestamp));
                if (receivedMessage == null)
                {   // Timeout
                    return false;
                }

                if (typesToSkipHashSet.Contains(receivedMessage.Type))
                {   // Type of message to ignore
                    continue;
                }

                var receivedOfType = receivedMessage as ReceivedMessage<T>;
                Assert.That(receivedOfType, Is.Not.Null);
                if (predicate(receivedOfType))
                {
                    return true;
                }
            }
        }
    }
}
