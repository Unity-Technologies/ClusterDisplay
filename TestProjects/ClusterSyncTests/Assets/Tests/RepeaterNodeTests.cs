using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class RepeaterNodeTests
    {
        UdpAgent m_EmitterAgent;
        FrameDataSplitter m_EmitterFrameDataSplitter;
        EmitterStateWriter m_EmitterStateWriter;
        EventBus<TestData> m_EmitterEventBus;
        RepeaterNode m_RepeaterNode;
        EventBus<TestData> m_RepeaterEventBus;
        int m_ReceivedBusDataCount;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId1 = 1;
        const byte k_RepeaterId2 = 2;

        void SetUp(bool isBackup, byte renderNodeId)
        {
            var emitterAgentConfig = udpConfig;
            emitterAgentConfig.ReceivedMessagesType = EmitterNode.ReceiveMessageTypes.ToArray();
            m_EmitterAgent = new(emitterAgentConfig);
            m_EmitterFrameDataSplitter = new(m_EmitterAgent);
            m_EmitterStateWriter = new(false);
            m_EmitterEventBus = new(EventBusFlags.WriteToCluster);

            RepeaterStateReader.ClearOnLoadDataDelegates();
            var repeaterAgentConfig = udpConfig;
            repeaterAgentConfig.ReceivedMessagesType = RepeaterNode.ReceiveMessageTypes.ToArray();
            var nodeConfig = new ClusterNodeConfig
            {
                NodeId = k_RepeaterId1,
                HandshakeTimeout = NodeTestUtils.Timeout,
                CommunicationTimeout = NodeTestUtils.Timeout,
                RepeatersDelayed = false,
                Fence = FrameSyncFence.Network
            };

            m_RepeaterNode = new RepeaterNode(nodeConfig, new UdpAgent(repeaterAgentConfig), isBackup);
            m_RepeaterNode.RenderNodeId = renderNodeId;
            m_RepeaterEventBus = new(EventBusFlags.ReadFromCluster);
            m_RepeaterEventBus.Subscribe(testData =>
            {
                ++m_ReceivedBusDataCount;

                ulong effectiveFrameIndex = m_RepeaterNode.FrameIndex;
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
            });
        }

        [UnityTest]
        public IEnumerator StatesTransitions()
        {
            SetUp(false, k_RepeaterId1);

            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            PublishEvents();
            m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var emittersJobForFrame0 = Task.Run(() =>
            {
                // Receive repeaters registration message
                using var receivedRegisteringMessage = m_EmitterAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(receivedRegisteringMessage, Is.Not.Null);
                Assert.That(receivedRegisteringMessage.Payload.NodeId, Is.EqualTo(k_RepeaterId1));

                // Answer
                m_EmitterAgent.SendMessage(MessageType.RepeaterRegistered, new RepeaterRegistered
                {
                    NodeId = receivedRegisteringMessage.Payload.NodeId,
                    IPAddressBytes = receivedRegisteringMessage.Payload.IPAddressBytes,
                    Accepted = true
                });

                // Receive message about ready to start frame
                using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(0));
                Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                // Answer
                m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                    new EmitterWaitingToStartFrame {FrameIndex = 0});

                // Transmit the first frame
                minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                m_EmitterStateWriter.PublishCurrentState(0, m_EmitterFrameDataSplitter);
            });

            m_RepeaterNode.DoFrame();
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(emittersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));
            Assert.That(m_ReceivedBusDataCount, Is.EqualTo(2));

            // ======= End of Frame ===========
            m_RepeaterNode.ConcludeFrame();
            yield return null;

            // ======= Frame 1 -> 4 ===========
            for (ulong frameIdx = 1; frameIdx < 5; ++frameIdx)
            {
                PublishEvents();
                m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
                ulong localFrameIndex = frameIdx; // To avoid "captured variable is modified in the outer scope" warnings
                minimalDoFrameExitTimestamp = long.MaxValue;
                var repeatersJobForFrame = Task.Run(() =>
                {
                    // A little sleep to be sure repeater is really waiting after the emitter
                    Thread.Sleep(50);

                    // Receive message about ready to start frame
                    using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                    Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                    Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(localFrameIndex));
                    Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                    Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                    // Answer
                    m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                        new EmitterWaitingToStartFrame {FrameIndex = localFrameIndex});

                    // Transmit the frame data
                    minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                    m_EmitterStateWriter.PublishCurrentState(localFrameIndex, m_EmitterFrameDataSplitter);
                });

                ConsumeAllReceivedEmitterMessages(m_EmitterAgent);

                m_RepeaterNode.DoFrame();
                doFrameExitTimestamp = Stopwatch.GetTimestamp();
                Assert.DoesNotThrow(repeatersJobForFrame.Wait);
                Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));
                Assert.That(m_ReceivedBusDataCount, Is.EqualTo((frameIdx + 1) * 2));

                // ======= End of Frame ===========
                m_RepeaterNode.ConcludeFrame();
                yield return null;
            }
        }

        static readonly IReadOnlyList<ClusterTopologyEntry> k_ChangeBackupToRepeaterTopology = new ClusterTopologyEntry[]
        {
            new () {NodeId = k_EmitterId, NodeRole = NodeRole.Emitter, RenderNodeId = k_EmitterId},
            new () {NodeId = k_RepeaterId1, NodeRole = NodeRole.Repeater, RenderNodeId = k_RepeaterId1},
        };
        static readonly int[] k_ChangeBackupToRepeaterBeforeFrames = {0, 1, 2, 10};

        [UnityTest]
        public IEnumerator ChangeBackupToRepeater(
            [ValueSource(nameof(k_ChangeBackupToRepeaterBeforeFrames))] int changeBackupToRepeaterBeforeFrame)
        {
            NodeRole expectedNodeRole = NodeRole.Backup;
            byte expectedRenderNodeId = 42;
            SetUp(true, expectedRenderNodeId);

            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var emittersJobForFrame0 = Task.Run(() =>
            {
                // Receive repeaters registration message
                using var receivedRegisteringMessage = m_EmitterAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(receivedRegisteringMessage, Is.Not.Null);
                Assert.That(receivedRegisteringMessage.Payload.NodeId, Is.EqualTo(k_RepeaterId1));

                // Answer
                m_EmitterAgent.SendMessage(MessageType.RepeaterRegistered, new RepeaterRegistered
                {
                    NodeId = receivedRegisteringMessage.Payload.NodeId,
                    IPAddressBytes = receivedRegisteringMessage.Payload.IPAddressBytes,
                    Accepted = true
                });

                // Receive message about ready to start frame
                using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(0));
                Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                // Answer
                m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                    new EmitterWaitingToStartFrame {FrameIndex = 0});

                // Transmit the first frame
                minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                m_EmitterStateWriter.PublishCurrentState(0, m_EmitterFrameDataSplitter);
            });

            if (changeBackupToRepeaterBeforeFrame <= 0)
            {
                m_RepeaterNode.UpdatedClusterTopology.Entries = k_ChangeBackupToRepeaterTopology;
                expectedNodeRole = NodeRole.Repeater;
                expectedRenderNodeId = k_RepeaterId1;
            }
            m_RepeaterNode.DoFrame();
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(emittersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));

            // ======= End of Frame ===========
            m_RepeaterNode.ConcludeFrame();
            Assert.That(m_RepeaterNode.NodeRole, Is.EqualTo(expectedNodeRole));
            Assert.That(m_RepeaterNode.RenderNodeId, Is.EqualTo(expectedRenderNodeId));
            yield return null;

            // ======= Frame 1 -> 4 ===========
            for (ulong frameIdx = 1; frameIdx < 5; ++frameIdx)
            {
                m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
                ulong localFrameIndex = frameIdx; // To avoid "captured variable is modified in the outer scope" warnings
                minimalDoFrameExitTimestamp = long.MaxValue;
                var repeatersJobForFrame = Task.Run(() =>
                {
                    // A little sleep to be sure repeater is really waiting after the emitter
                    Thread.Sleep(50);

                    // Receive message about ready to start frame
                    using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                    Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                    Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(localFrameIndex));
                    Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                    Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                    // Answer
                    m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                        new EmitterWaitingToStartFrame {FrameIndex = localFrameIndex});

                    // Transmit the frame data
                    minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                    m_EmitterStateWriter.PublishCurrentState(localFrameIndex, m_EmitterFrameDataSplitter);
                });

                ConsumeAllReceivedEmitterMessages(m_EmitterAgent);

                if (changeBackupToRepeaterBeforeFrame <= (int)frameIdx)
                {
                    m_RepeaterNode.UpdatedClusterTopology.Entries = k_ChangeBackupToRepeaterTopology;
                    expectedNodeRole = NodeRole.Repeater;
                    expectedRenderNodeId = k_RepeaterId1;
                }
                m_RepeaterNode.DoFrame();
                doFrameExitTimestamp = Stopwatch.GetTimestamp();
                Assert.DoesNotThrow(repeatersJobForFrame.Wait);
                Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));

                // ======= End of Frame ===========
                m_RepeaterNode.ConcludeFrame();
                Assert.That(m_RepeaterNode.NodeRole, Is.EqualTo(expectedNodeRole));
                Assert.That(m_RepeaterNode.RenderNodeId, Is.EqualTo(expectedRenderNodeId));
                yield return null;
            }
        }

        static readonly IReadOnlyList<ClusterTopologyEntry> k_ChangeBackupToEmitterTopology = new ClusterTopologyEntry[]
        {
            new () {NodeId = k_RepeaterId1, NodeRole = NodeRole.Emitter, RenderNodeId = k_RepeaterId1},
            new () {NodeId = k_RepeaterId2, NodeRole = NodeRole.Repeater, RenderNodeId = k_RepeaterId2},
        };

        [UnityTest]
        public IEnumerator ChangeBackupToEmitter()
        {
            SetUp(true, 42);

            // Setup a UdpAgent that will be used to fake a late Repeater2
            var emitterAgentConfig = udpConfig;
            emitterAgentConfig.ReceivedMessagesType = RepeaterNode.ReceiveMessageTypes.ToArray();
            using UdpAgent repeater2Agent = new(emitterAgentConfig);

            long testEndTimestamp = StopwatchUtils.TimestampIn(TimeSpan.FromSeconds(15));

            // ======== First Frame ======
            m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
            long minimalDoFrameExitTimestamp = long.MaxValue;
            var emittersJobForFrame0 = Task.Run(() =>
            {
                // Receive repeaters registration message
                using var receivedRegisteringMessage = m_EmitterAgent.TryConsumeNextReceivedMessage(
                        StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(receivedRegisteringMessage, Is.Not.Null);
                Assert.That(receivedRegisteringMessage.Payload.NodeId, Is.EqualTo(k_RepeaterId1));

                // Answer
                m_EmitterAgent.SendMessage(MessageType.RepeaterRegistered, new RepeaterRegistered
                {
                    NodeId = receivedRegisteringMessage.Payload.NodeId,
                    IPAddressBytes = receivedRegisteringMessage.Payload.IPAddressBytes,
                    Accepted = true
                });

                // Receive message about ready to start frame
                using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(0));
                Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                // Answer
                m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame,
                    new EmitterWaitingToStartFrame {FrameIndex = 0});

                // Transmit the first frame
                minimalDoFrameExitTimestamp = Stopwatch.GetTimestamp();
                m_EmitterStateWriter.PublishCurrentState(0, m_EmitterFrameDataSplitter);
            });

            m_RepeaterNode.DoFrame();
            long doFrameExitTimestamp = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(emittersJobForFrame0.Wait);
            Assert.That(doFrameExitTimestamp, Is.GreaterThanOrEqualTo(minimalDoFrameExitTimestamp));

            // ======= End of Frame ===========
            m_RepeaterNode.ConcludeFrame();
            Assert.That(m_RepeaterNode.FrameIndex, Is.EqualTo(1));
            ConsumeAllReceivedEmitterMessages(m_EmitterAgent);
            yield return null;

            // Change the cluster topology
            m_RepeaterNode.UpdatedClusterTopology.Entries = k_ChangeBackupToEmitterTopology;

            // ======= Second Frame ===========
            m_EmitterStateWriter.GatherFrameState(); // Has to be called from this thread, can't be done in a task
            var emittersJobForFrame1 = Task.Run(() =>
            {
                // Receive message about ready to start frame
                using var receivedRepeaterWaiting = m_EmitterAgent.TryConsumeNextReceivedMessage(
                    StopwatchUtils.TimeUntil(testEndTimestamp)) as ReceivedMessage<RepeaterWaitingToStartFrame>;
                Assert.That(receivedRepeaterWaiting, Is.Not.Null);
                Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(1));
                Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_RepeaterId1));
                Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.True);

                // But do not answer...  We want to validate everything is in place to transition from backup to emitter,
                // so let that emitter mock play dead...
            });

            // Do frame should stay stuck until Repeater2 did catch up with Repeater1.  So start it in a task.
            var repeater1DoFrame1Task = Task.Run(() =>
            {
                m_RepeaterNode.DoFrame();
            });
            // The dead emitter should have received a ping from the repeater signaling it is waiting to start next
            // frame.
            Assert.DoesNotThrow(emittersJobForFrame1.Wait);

            // Wait a little bit so that a problem causing DoFrame to finish regardless of the dead emitter would be
            // detected.
            Thread.Sleep(100);
            Assert.That(repeater1DoFrame1Task.IsCompleted, Is.False);

            // Repeater2 should receive a survey
            using var receivedSurveyRepeatersMessage = repeater2Agent.ConsumeMessagesUntil<SurveyRepeaters>(
                TimeSpan.FromSeconds(15), _ => true);
            ConsumeAllReceivedEmitterMessages(repeater2Agent);
            repeater2Agent.SendMessage(MessageType.RepeatersSurveyAnswer, new RepeatersSurveyAnswer() {
                NodeId = k_RepeaterId2, IPAddressBytes = 0, // Not used for this test
                LastReceivedFrameIndex = 0, // Claim we receive frame 0, this should be enough to unblock the do frame
                StillUseNetworkSync = false
            });

            // And do frame should now complete
            Assert.DoesNotThrow(repeater1DoFrame1Task.Wait);

            // And the RepeaterNode should now indicate it wants to switch from backup to emitter
            Assert.That(m_RepeaterNode.IsBackupToEmitterSwitchReady(), Is.True);
            var repeatersSurveyResult = m_RepeaterNode.RepeatersSurveyResult;
            Assert.That(repeatersSurveyResult.FirstOrDefault(r => r.NodeId == k_RepeaterId1), Is.Not.Null);
            Assert.That(repeatersSurveyResult.First(r => r.NodeId == k_RepeaterId1).StillUseNetworkSync, Is.True);
            Assert.That(repeatersSurveyResult.FirstOrDefault(r => r.NodeId == k_RepeaterId2), Is.Not.Null);
            Assert.That(repeatersSurveyResult.First(r => r.NodeId == k_RepeaterId2).StillUseNetworkSync, Is.False);
        }

        static void ConsumeAllReceivedEmitterMessages(IUdpAgent agent)
        {
            for (;;)
            {
                using var receivedMessage = agent.TryConsumeNextReceivedMessage();
                if (receivedMessage == null)
                {
                    break;
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            m_RepeaterEventBus?.Dispose();
            m_RepeaterNode?.Dispose();
            m_EmitterEventBus?.Dispose();
            m_EmitterStateWriter?.Dispose();
            m_EmitterFrameDataSplitter?.Dispose();
            m_EmitterAgent?.Dispose();
        }

        void PublishEvents()
        {
            m_EmitterEventBus.Publish(new TestData
            {
                EnumVal = StateID.Random,
                LongVal = (long)(m_RepeaterNode.FrameIndex * m_RepeaterNode.FrameIndex),
                FloatVal = m_RepeaterNode.FrameIndex,
            });

            m_EmitterEventBus.Publish(new TestData
            {
                EnumVal = StateID.Input,
                LongVal = (long)(m_RepeaterNode.FrameIndex * m_RepeaterNode.FrameIndex) + 1,
                FloatVal = m_RepeaterNode.FrameIndex,
            });
        }
    }
}
