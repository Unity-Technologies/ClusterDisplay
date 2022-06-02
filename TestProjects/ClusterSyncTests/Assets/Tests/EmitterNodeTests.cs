using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;
using Task = System.Threading.Tasks.Task;

namespace Unity.ClusterDisplay.Tests
{
    public class EmitterNodeTests
    {
        const byte k_EmitterId = 0;
        static readonly byte[] k_RepeaterIds = {1, 2};

        // Various per repeater data necessary to mimic it or validate what it receives from the emitter.
        class RepeaterData: IDisposable
        {
            public byte NodeId;
            public UDPAgent UdpAgent;
            public FrameDataAssembler Assembler;
            public EventBus<TestData> EventBus;
            public int ReceivedTestData;
            public void Dispose()
            {
                Assembler?.Dispose();
                UdpAgent?.Dispose();
                EventBus?.Dispose();
            }

            public void ProcessFrameData(IReceivedMessageExtraData extraData)
            {
                foreach (var frameData in new FrameDataReader(extraData.AsNativeArray()))
                {
                    if (frameData.id == (int)StateID.CustomEvents)
                    {
                        EventBus.DeserializeAndPublish(frameData.data);
                    }
                }
            }
        }

        RepeaterData[] m_Repeaters;
        EmitterNode m_Node;
        EventBus<TestData> m_NodeEventBus;

        void SetUp(bool repeatersDelayed)
        {
            m_Repeaters = k_RepeaterIds.Select( nodeId =>
            {
                var ret = new RepeaterData();
                var testConfig = udpConfig;
                testConfig.ReceivedMessagesType = RepeaterNode.ReceiveMessageTypes.ToArray();
                ret.NodeId = nodeId;
                ret.UdpAgent = new UDPAgent(testConfig);
                ret.Assembler = new FrameDataAssembler(ret.UdpAgent, false);
                ret.EventBus = new EventBus<TestData>(EventBusFlags.ReadFromCluster);
                ret.EventBus.Subscribe(data =>
                {
                    ++ret.ReceivedTestData;
                    TestEvent(data);
                });
                return ret;
            }).ToArray();

            var emitterUdpConfig = udpConfig;
            emitterUdpConfig.ReceivedMessagesType = EmitterNode.ReceiveMessageTypes.ToArray();
            var nodeConfig = new ClusterNodeConfig
            {
                NodeId = k_EmitterId,
                HandshakeTimeout = NodeTestUtils.Timeout,
                CommunicationTimeout = NodeTestUtils.Timeout,
                RepeatersDelayed = repeatersDelayed
            };

            var emitterNodeConfig = new EmitterNodeConfig
            {
                ExpectedRepeaterCount = (byte)k_RepeaterIds.Length
            };

            m_Node = new EmitterNode(nodeConfig, emitterNodeConfig, new UDPAgent(emitterUdpConfig));

            // Clear previous EventBus from previous tests
            EmitterStateWriter.ClearCustomDataDelegates();

            // Setup emitter's EventBus
            m_NodeEventBus = new EventBus<TestData>(EventBusFlags.WriteToCluster);
        }

        [UnityTest]
        public IEnumerator StatesTransitionsWithNetworkSync()
        {
            SetUp(false);
            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var repeatersJobForFrame0 = Task.Run(() =>
            {
                // A little sleep to be sure emitter is really waiting after repeaters
                Thread.Sleep(50);

                // Inform repeaters are present
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RegisteringWithEmitter,
                        new RegisteringWithEmitter
                    {
                        NodeId = k_RepeaterIds[repeaterIndex],
                        IPAddressBytes = BitConverter.ToUInt32(m_Repeaters[repeaterIndex].UdpAgent.AdapterAddress.GetAddressBytes())
                    });
                }

                // Receive answers
                int registeredRepeaters = ConsumeFirstMessageOfRepeaterWhere<RepeaterRegistered>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        return message.Payload.NodeId == repeaterData.NodeId;
                    });
                Assert.That(registeredRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // Emitter should then wait for us to inform we are ready for the next (first) frame (network based
                // synchronization). Sleep a little bit to be sure emitter is not just skipping through steps without
                // waiting...
                Thread.Sleep(50);

                // Clear possible remaining RepeaterRegistered messages still waiting (that were simply targeted to other
                // repeaters (but that each repeater still received since it is sent in multicast)).
                ClearRepeatersMessagesOfType(MessageType.RepeaterRegistered);

                // Inform repeaters ready to start frame
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame,
                        new RepeaterWaitingToStartFrame
                        {
                            FrameIndex = 0,
                            NodeId = k_RepeaterIds[repeaterIndex],
                            WillUseNetworkSyncOnNextFrame = true
                        });
                }

                // Receive answers
                int acknowledgedRepeaters = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                    testEndTimestamp, (message, repeaterData) => TestNotWaitingOnNodeId(message.Payload, repeaterData.NodeId));
                Assert.That(acknowledgedRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // We should now be receiving FrameData for FrameIndex 0
                int repeatersWithFrame0 = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        Assert.That(message.Payload.FrameIndex, Is.EqualTo(0));
                        // FrameDataAssembler should have assembled everything into a single frame
                        Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                        Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                        repeaterData.ProcessFrameData(message.ExtraData);
                        return true;
                    }, new[]{ MessageType.EmitterWaitingToStartFrame });
                Assert.That(repeatersWithFrame0, Is.EqualTo(k_RepeaterIds.Length));
            });

            PublishEvents();
            m_Node.DoFrame();
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeatersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));
            foreach (var repeaterData in m_Repeaters)
            {
                Assert.That(repeaterData.ReceivedTestData, Is.EqualTo(2));
            }

            // ======= End of Frame ===========
            m_Node.ConcludeFrame();
            yield return null;

            // ======= Frame 1 -> 4 ===========
            for (ulong frameIdx = 1; frameIdx < 5; ++frameIdx)
            {
                ulong localFrameIndex = frameIdx; // To avoid "captured variable is modified in the outer scope" warnings
                minimalDoFrameExitTimestamp = long.MaxValue;
                var repeatersJobForFrame = Task.Run(() =>
                {
                    // A little sleep to be sure emitter is really waiting after repeaters
                    Thread.Sleep(50);

                    // Inform repeaters ready to start frame
                    Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                    for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                    {
                        minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                        m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame,
                            new RepeaterWaitingToStartFrame
                            {
                                FrameIndex = localFrameIndex,
                                NodeId = k_RepeaterIds[repeaterIndex],
                                WillUseNetworkSyncOnNextFrame = true
                            });
                    }

                    // Receive answers
                    int acknowledgedRepeaters = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                        testEndTimestamp, (message, repeaterData) =>
                        {
                            Assert.That(message.Payload.FrameIndex, Is.EqualTo(localFrameIndex));
                            return TestNotWaitingOnNodeId(message.Payload, repeaterData.NodeId);
                        });
                    Assert.That(acknowledgedRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                    // We should now be receiving FrameData for frameIdx
                    int repeatersWithFrame = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                        testEndTimestamp, (message, repeaterData) =>
                        {
                            Assert.That(message.Payload.FrameIndex, Is.EqualTo(localFrameIndex));
                            // FrameDataAssembler should have assembled everything into a single frame
                            Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                            Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                            repeaterData.ProcessFrameData(message.ExtraData);
                            return true;
                        }, new[] {MessageType.EmitterWaitingToStartFrame});
                    Assert.That(repeatersWithFrame, Is.EqualTo(k_RepeaterIds.Length));
                });

                PublishEvents();
                m_Node.DoFrame();
                doFrameExitTimestamp = Stopwatch.GetTimestamp();
                Assert.DoesNotThrow(repeatersJobForFrame.Wait);
                Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));
                foreach (var repeaterData in m_Repeaters)
                {
                    Assert.That(repeaterData.ReceivedTestData, Is.EqualTo((frameIdx + 1) * 2));
                }

                // ======= End of Frame ===========
                m_Node.ConcludeFrame();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator StatesTransitionsWithHardwareSync()
        {
            SetUp(false);
            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var repeatersJobForFrame0 = Task.Run(() =>
            {
                // A little sleep to be sure emitter is really waiting after repeaters
                Thread.Sleep(50);

                // Inform repeaters are present
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RegisteringWithEmitter,
                        new RegisteringWithEmitter
                    {
                        NodeId = k_RepeaterIds[repeaterIndex],
                        IPAddressBytes =
                            BitConverter.ToUInt32(m_Repeaters[repeaterIndex].UdpAgent.AdapterAddress.GetAddressBytes())
                    });
                }

                // Receive answers
                int registeredRepeaters = ConsumeFirstMessageOfRepeaterWhere<RepeaterRegistered>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        return message.Payload.NodeId == repeaterData.NodeId;
                    });
                Assert.That(registeredRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // Emitter should then wait for us to inform we are ready for the next (first) frame (network based
                // synchronization). Sleep a little bit to be sure emitter is not just skipping through steps without
                // waiting...
                Thread.Sleep(50);

                // Clear possible remaining RepeaterRegistered messages still waiting (that were simply targeted to other
                // repeaters (but that each repeater still received since it is sent in multicast)).
                ClearRepeatersMessagesOfType(MessageType.RepeaterRegistered);

                // Inform repeaters ready to start frame
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame,
                        new RepeaterWaitingToStartFrame
                        {
                            FrameIndex = 0,
                            NodeId = k_RepeaterIds[repeaterIndex],
                            WillUseNetworkSyncOnNextFrame = false
                        });
                }

                // Receive answers
                int acknowledgedRepeaters = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                    testEndTimestamp, (message, repeaterData) => TestNotWaitingOnNodeId(message.Payload, repeaterData.NodeId));
                Assert.That(acknowledgedRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // We should now be receiving FrameData for FrameIndex 0
                int repeatersWithFrame0 = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        Assert.That(message.Payload.FrameIndex, Is.EqualTo(0));
                        // FrameDataAssembler should have assembled everything into a single frame
                        Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                        Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                        repeaterData.ProcessFrameData(message.ExtraData);
                        return true;
                    }, new[]{ MessageType.EmitterWaitingToStartFrame });
                Assert.That(repeatersWithFrame0, Is.EqualTo(k_RepeaterIds.Length));
            });

            PublishEvents();
            m_Node.DoFrame();
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeatersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));
            foreach (var repeaterData in m_Repeaters)
            {
                Assert.That(repeaterData.ReceivedTestData, Is.EqualTo(2));
            }

            // ======= End of Frame ===========
            m_Node.ConcludeFrame();
            yield return null;

            // ======= Frame 1 -> 4 ===========
            for (ulong frameIdx = 1; frameIdx < 5; ++frameIdx)
            {
                ulong localFrameIndex = frameIdx; // To avoid "captured variable is modified in the outer scope" warnings

                var repeatersJobForFrame = Task.Run(() =>
                {
                    // A little sleep to be sure emitter is really waiting after repeaters
                    Thread.Sleep(50);

                    // We should now be receiving FrameData for frameIdx
                    int repeatersWithFrame = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                        testEndTimestamp, (message, repeaterData) =>
                        {
                            Assert.That(message.Payload.FrameIndex, Is.EqualTo(localFrameIndex));
                            // FrameDataAssembler should have assembled everything into a single frame
                            Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                            Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                            repeaterData.ProcessFrameData(message.ExtraData);
                            return true;
                        });
                    Assert.That(repeatersWithFrame, Is.EqualTo(k_RepeaterIds.Length));
                });

                PublishEvents();
                m_Node.DoFrame();
                Assert.DoesNotThrow(repeatersJobForFrame.Wait);
                foreach (var repeaterData in m_Repeaters)
                {
                    Assert.That(repeaterData.ReceivedTestData, Is.EqualTo((frameIdx + 1) * 2));
                }

                // ======= End of Frame ===========
                m_Node.ConcludeFrame();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator StatesTransitionsDelayedRepeater()
        {
            SetUp(true);
            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var repeatersJobForFrame0 = Task.Run(() =>
            {
                // A little sleep to be sure emitter is really waiting after repeaters
                Thread.Sleep(50);

                // Inform repeaters are present
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RegisteringWithEmitter,
                        new RegisteringWithEmitter
                    {
                        NodeId = k_RepeaterIds[repeaterIndex],
                        IPAddressBytes =
                            BitConverter.ToUInt32(m_Repeaters[repeaterIndex].UdpAgent.AdapterAddress.GetAddressBytes())
                    });
                }

                // Receive answers
                int registeredRepeaters = ConsumeFirstMessageOfRepeaterWhere<RepeaterRegistered>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        return message.Payload.NodeId == repeaterData.NodeId;
                    });
                Assert.That(registeredRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // Emitter should then wait for us to inform we are ready for the next (first) frame (network based
                // synchronization). Sleep a little bit to be sure emitter is not just skipping through steps without
                // waiting...
                Thread.Sleep(50);

                // Clear possible remaining RepeaterRegistered messages still waiting (that were simply targeted to other
                // repeaters (but that each repeater still received since it is sent in multicast)).
                ClearRepeatersMessagesOfType(MessageType.RepeaterRegistered);

                // Inform repeaters ready to start frame
                Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                {
                    minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                    m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame,
                        new RepeaterWaitingToStartFrame
                        {
                            FrameIndex = 0,
                            NodeId = k_RepeaterIds[repeaterIndex],
                            WillUseNetworkSyncOnNextFrame = true
                        });
                }

                // Receive answers
                int acknowledgedRepeaters = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                    testEndTimestamp, (message, repeaterData) => TestNotWaitingOnNodeId(message.Payload, repeaterData.NodeId));
                Assert.That(acknowledgedRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                // We should now be receiving FrameData for FrameIndex 0
                int repeatersWithFrame0 = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                    testEndTimestamp, (message, repeaterData) =>
                    {
                        Assert.That(message.Payload.FrameIndex, Is.EqualTo(0));
                        // FrameDataAssembler should have assembled everything into a single frame
                        Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                        Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                        repeaterData.ProcessFrameData(message.ExtraData);
                        return true;
                    }, new[]{ MessageType.EmitterWaitingToStartFrame });
                Assert.That(repeatersWithFrame0, Is.EqualTo(k_RepeaterIds.Length));
            });

            m_Node.DoFrame();
            PublishEvents(); // After DoFrame since delayed
            Assert.That(minimalDoFrameExitTimestamp, Is.EqualTo(long.MaxValue));
            Assert.That(repeatersJobForFrame0.IsCompleted, Is.False);

            // ======= End of Frame 0 for emitter ===========
            m_Node.ConcludeFrame();
            yield return null;

            // ======= End of Frame 1 for emitter ===========
            Assert.That(minimalDoFrameExitTimestamp, Is.EqualTo(long.MaxValue));
            Assert.That(repeatersJobForFrame0.IsCompleted, Is.False);
            m_Node.DoFrame();
            PublishEvents(); // After DoFrame since delayed
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeatersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));

            // ======= End of Frame ===========
            m_Node.ConcludeFrame();
            yield return null;

            // ======= Frame 2 -> 4 ===========
            for (ulong frameIdx = 2; frameIdx < 5; ++frameIdx)
            {
                ulong localFrameIndex = frameIdx; // To avoid "captured variable is modified in the outer scope" warnings

                minimalDoFrameExitTimestamp = long.MaxValue;
                var repeatersJobForFrame = Task.Run(() =>
                {
                    // A little sleep to be sure emitter is really waiting after repeaters
                    Thread.Sleep(50);

                    // Inform repeaters ready to start frame
                    Assert.That(m_Repeaters.Length, Is.EqualTo(k_RepeaterIds.Length));
                    for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
                    {
                        minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                        m_Repeaters[repeaterIndex].UdpAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame,
                            new RepeaterWaitingToStartFrame
                            {
                                FrameIndex = localFrameIndex - 1,
                                NodeId = k_RepeaterIds[repeaterIndex],
                                WillUseNetworkSyncOnNextFrame = true
                            });
                    }

                    // Receive answers
                    int acknowledgedRepeaters = ConsumeFirstMessageOfRepeaterWhere<EmitterWaitingToStartFrame>(
                        testEndTimestamp, (message, repeaterData) =>
                        {
                            Assert.That(message.Payload.FrameIndex, Is.EqualTo(localFrameIndex - 1));

                            return TestNotWaitingOnNodeId(message.Payload, repeaterData.NodeId);
                        });
                    Assert.That(acknowledgedRepeaters, Is.EqualTo(k_RepeaterIds.Length));

                    // We should now be receiving FrameData for frameIdx - 1
                    int repeatersWithFrame = ConsumeFirstMessageOfRepeaterWhere<FrameData>(
                        testEndTimestamp, (message, repeaterData) =>
                        {
                            Assert.That(message.Payload.FrameIndex, Is.EqualTo(localFrameIndex - 1));
                            // FrameDataAssembler should have assembled everything into a single frame
                            Assert.That(message.Payload.DatagramDataOffset, Is.Zero);
                            Assert.That(message.Payload.DataLength, Is.EqualTo(message.ExtraData.Length));

                            repeaterData.ProcessFrameData(message.ExtraData);
                            return true;
                        }, new[] {MessageType.EmitterWaitingToStartFrame});
                    Assert.That(repeatersWithFrame, Is.EqualTo(k_RepeaterIds.Length));
                });

                m_Node.DoFrame();
                PublishEvents(); // After DoFrame since delayed
                doFrameExitTimestamp = Stopwatch.GetTimestamp();
                Assert.DoesNotThrow(repeatersJobForFrame.Wait);
                Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));

                // ======= End of Frame ===========
                m_Node.ConcludeFrame();
                yield return null;
            }
        }

        void ClearRepeatersMessagesOfType(MessageType messageType)
        {
            for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
            {
                ClearMessagesOfType(m_Repeaters[repeaterIndex].UdpAgent, messageType);
            }
        }

        static void ClearMessagesOfType(IUDPAgent udpAgent, MessageType messageType)
        {
            for (;;)
            {
                using var receivedMessage = udpAgent.TryConsumeNextReceivedMessage();
                if (receivedMessage != null)
                {
                    Assert.That(receivedMessage.Type, Is.EqualTo(messageType));
                }
                else
                {
                    return;
                }
            }
        }

        int ConsumeFirstMessageOfRepeaterWhere<T>(long testEndTimestamp, Func<ReceivedMessage<T>, RepeaterData, bool> predicate,
            IEnumerable<MessageType> typesToSkip = null) where T: unmanaged
        {
            var typesToSkipHashSet = new HashSet<MessageType>();
            if (typesToSkip != null)
            {
                foreach (var type in typesToSkip)
                {
                    typesToSkipHashSet.Add(type);
                }
            }

            int consumedCount = 0;
            for (int repeaterIndex = 0; repeaterIndex < k_RepeaterIds.Length; ++repeaterIndex)
            {
                for (;;)
                {
                    using var receivedMessage = m_Repeaters[repeaterIndex].UdpAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp));
                    if (receivedMessage == null)
                    {   // Timeout
                        break;
                    }

                    if (typesToSkipHashSet.Contains(receivedMessage.Type))
                    {   // Type of message to ignore
                        continue;
                    }

                    var receivedOfType = receivedMessage as ReceivedMessage<T>;
                    Assert.That(receivedOfType, Is.Not.Null);
                    if (predicate(receivedOfType, m_Repeaters[repeaterIndex]))
                    {
                        ++consumedCount;
                        break;
                    }
                }
            }
            return consumedCount;
        }

        static unsafe bool TestNotWaitingOnNodeId(EmitterWaitingToStartFrame emitterWaitingToStart, byte nodeId)
        {
            ulong nodeIdBit = 1ul << nodeId;
            return (emitterWaitingToStart.WaitingOn[nodeId >> 6] & nodeIdBit) == 0;
        }

        [TearDown]
        public void TearDown()
        {
            m_NodeEventBus?.Dispose();
            m_Node?.Dispose();
            foreach (var repeater in m_Repeaters)
            {
                repeater.Dispose();
            }
        }

        void PublishEvents()
        {
            m_NodeEventBus.Publish(new TestData
            {
                EnumVal = StateID.Random,
                LongVal = (long)(m_Node.FrameIndex * m_Node.FrameIndex),
                FloatVal = m_Node.FrameIndex,
            });

            m_NodeEventBus.Publish(new TestData
            {
                EnumVal = StateID.Input,
                LongVal = (long)(m_Node.FrameIndex * m_Node.FrameIndex) + 1,
                FloatVal = m_Node.FrameIndex,
            });
        }

        void TestEvent(TestData testData)
        {
            ulong effectiveFrameIndex = m_Node.FrameIndex;
            if (m_Node.Config.RepeatersDelayed)
            {
                Assert.That(m_Node.FrameIndex, Is.GreaterThan(0));
                --effectiveFrameIndex;
            }
            switch (testData.EnumVal)
            {
                case StateID.Random:
                    Assert.That(testData.LongVal, Is.EqualTo(effectiveFrameIndex * effectiveFrameIndex));
                    break;
                case StateID.Input:
                    Assert.That(testData.LongVal, Is.EqualTo(effectiveFrameIndex * effectiveFrameIndex + 1));
                    break;
                default:
                    Assert.Fail("Unexpected testData.EnumVal");
                    break;
            }
            Assert.That(testData.FloatVal, Is.EqualTo(effectiveFrameIndex));
        }
    }
}
