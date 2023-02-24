using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Unity.ClusterDisplay.Tests
{
    public class FrameWaitingToStartHandlerTests
    {
        [Test]
        public void WaitingOnSingleFrame()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, 0, new NodeIdBitVectorReadOnly(new byte[]{1,3}));

            long beforeSecondRepeaterTimestamp = long.MaxValue;
            var repeaterTask = Task.Run(() =>
            {
                SendRepeaterWaiting(0, 3, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode3 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode3, 0, new byte[] {1});

                Thread.Sleep(250);

                beforeSecondRepeaterTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaiting(0, 1, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode1 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode1, 0, new byte[] {});
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeaterTask.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeSecondRepeaterTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
        }

        [Test]
        public void IgnoreDoubleSet()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, 0, new NodeIdBitVectorReadOnly(new byte[]{1,3,9}));

            long beforeThirdRepeaterTimestamp = long.MaxValue;
            var repeaterTask = Task.Run(() =>
            {
                SendRepeaterWaiting(0, 3, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode3 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode3, 0, new byte[] {1,9});

                SendRepeaterWaiting(0, 9, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode9 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode9, 0, new byte[] {1});

                SendRepeaterWaiting(0, 3, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode3Take2 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode3Take2, 0, new byte[] {1});

                Thread.Sleep(250);

                beforeThirdRepeaterTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaiting(0, 1, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode1 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode1, 0, new byte[] {});
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeaterTask.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeThirdRepeaterTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
        }

        [Test]
        public void RepeatersStopsWaiting()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, 0, new NodeIdBitVectorReadOnly(new byte[]{2,7}));

            var repeaterTask1 = Task.Run(() =>
            {
                SendRepeaterWaiting(0, 2, false, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode2 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode2, 0, new byte[] {7});

                SendRepeaterWaiting(0, 7, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode7 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode7, 0, new byte[] {});
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            Assert.DoesNotThrow(repeaterTask1.Wait);
            Assert.That(stillWaitingOn, Is.Null);

            bool ret = waitingToStartHandler.PrepareForNextFrame(1);
            Assert.That(ret, Is.True);

            var repeaterTask2 = Task.Run(() =>
            {
                SendRepeaterWaiting(1, 7, false, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode7 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode7, 1, new byte[] {});
            });

            stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(1, m_MaxTestTime);
            Assert.DoesNotThrow(repeaterTask2.Wait);
            Assert.That(stillWaitingOn, Is.Null);

            ret = waitingToStartHandler.PrepareForNextFrame(2);
            Assert.That(ret, Is.False);

            stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(2, m_MaxTestTime);
            Assert.That(stillWaitingOn, Is.Null);
        }

        [Test]
        public void MessagesFromWrongFrameIgnored()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, 0, new NodeIdBitVectorReadOnly(new byte[]{2,7}));

            var repeaterTask1 = Task.Run(() =>
            {
                SendRepeaterWaiting(0, 2, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode2 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode2, 0, new byte[] {7});

                SendRepeaterWaiting(0, 7, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode7 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode7, 0, new byte[] {});
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            Assert.DoesNotThrow(repeaterTask1.Wait);
            Assert.That(stillWaitingOn, Is.Null);

            bool ret = waitingToStartHandler.PrepareForNextFrame(1);
            Assert.That(ret, Is.True);

            long beforeSendingRightFramesTimestamp = long.MaxValue;
            var repeaterTask2 = Task.Run(() =>
            {
                // Send messages for "old frames", we are expecting answers telling us everything is ready for this past
                // frame.
                SendRepeaterWaiting(0, 2, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using (var receivedMessage = repeaterAgent.ConsumeNextReceivedMessage())
                {
                    TestEmitterWaitingMessage(receivedMessage, 0, new byte[] {});
                }

                SendRepeaterWaiting(0, 7, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using (var receivedMessage = repeaterAgent.ConsumeNextReceivedMessage())
                {
                    TestEmitterWaitingMessage(receivedMessage, 0, new byte[] {});
                }

                // Send messages for "future frames", we are simply expecting no answer
                SendRepeaterWaiting(2, 2, true, repeaterAgent);
                LogAssert.Expect(LogType.Error, new Regex("Unexpected exception pre-processing received messages:.*"));
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(0));

                SendRepeaterWaiting(2, 7, true, repeaterAgent);
                LogAssert.Expect(LogType.Error, new Regex("Unexpected exception pre-processing received messages:.*"));
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(0));

                // And now check that everything is still in a good state by sending the right frame.
                Thread.Sleep(250);
                beforeSendingRightFramesTimestamp = Stopwatch.GetTimestamp();

                SendRepeaterWaiting(1, 7, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode7 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode7, 1, new byte[] {2});

                SendRepeaterWaiting(1, 2, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode2 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode2, 1, new byte[] {});
            });

            stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(1, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeaterTask2.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeSendingRightFramesTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
        }

        [Test]
        public void StopsWaitingForRepeatersRemovedFromTopology()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            ClusterTopology clusterTopology = new();
            using var waitingToStartHandler = new FrameWaitingToStartHandler(emitterAgent, 0,
                new NodeIdBitVectorReadOnly(new byte[]{1,3}), 0, clusterTopology);

            long beforeTopologyChangeTimestamp = long.MaxValue;
            var repeaterTask1 = Task.Run(() =>
            {
                SendRepeaterWaiting(0, 3, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode3 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode3, 0, new byte[] {1});

                Thread.Sleep(250);

                List<ClusterTopologyEntry> newTopology = new();
                newTopology.Add(new(){NodeId = 0, NodeRole = NodeRole.Emitter, RenderNodeId = 0});
                newTopology.Add(new(){NodeId = 3, NodeRole = NodeRole.Repeater, RenderNodeId = 3});
                // 1 is not present in the list of entries anymore, so removing it...

                beforeTopologyChangeTimestamp = Stopwatch.GetTimestamp();
                clusterTopology.Entries = newTopology;
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeaterTask1.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeTopologyChangeTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));

            // Test that the second frame still wait after the repeater that is still in the topology
            bool ret = waitingToStartHandler.PrepareForNextFrame(1);
            Assert.That(ret, Is.True);

            long beforeRepeaterWaitingTimeStamp = long.MaxValue;
            var repeaterTask2 = Task.Run(() =>
            {
                Thread.Sleep(250);

                beforeRepeaterWaitingTimeStamp = Stopwatch.GetTimestamp();
                SendRepeaterWaiting(1, 3, true, repeaterAgent);
                Assert.That(repeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using var receivedMessageNode7 = repeaterAgent.ConsumeNextReceivedMessage();
                TestEmitterWaitingMessage(receivedMessageNode7, 1, new byte[] {});
            });

            stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(1, m_MaxTestTime);
            tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(repeaterTask2.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeRepeaterWaitingTimeStamp, Is.LessThanOrEqualTo(tryWaitEndTime));
        }

        [Test]
        public void StopsWaitingForRepeatersWhenNotEmitterAnymoreStillHasRepeaters()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            ClusterTopology clusterTopology = new();
            using var waitingToStartHandler = new FrameWaitingToStartHandler(emitterAgent, 0,
                new NodeIdBitVectorReadOnly(new byte[]{1,3}), 0, clusterTopology);

            long beforeTopologyChangeTimestamp = long.MaxValue;
            var changeTopologyTask = Task.Run(() =>
            {
                Thread.Sleep(250);

                List<ClusterTopologyEntry> newTopology = new();
                newTopology.Add(new(){NodeId = 1, NodeRole = NodeRole.Repeater, RenderNodeId = 1});
                newTopology.Add(new(){NodeId = 3, NodeRole = NodeRole.Emitter, RenderNodeId = 0});
                // 0 is not present in the list of entries anymore, so it is not an emitter anymore.

                beforeTopologyChangeTimestamp = Stopwatch.GetTimestamp();
                clusterTopology.Entries = newTopology;
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(changeTopologyTask.Wait);
            Assert.That(stillWaitingOn, Is.Not.Null);
            Assert.That(stillWaitingOn.SetBitsCount, Is.EqualTo(2));
            Assert.That(stillWaitingOn[1], Is.True);
            Assert.That(stillWaitingOn[3], Is.True); // Should still be waiting on 3 even if it is now an emitter
            Assert.That(beforeTopologyChangeTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
            Assert.That(tryWaitEndTime - beforeTopologyChangeTimestamp, Is.LessThan(Stopwatch.Frequency * 2));
        }

        [Test]
        public void StopsWaitingForRepeatersWhenNotEmitterAnymoreNoMoreRepeaters()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var emitterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUdpAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            ClusterTopology clusterTopology = new();
            using var waitingToStartHandler = new FrameWaitingToStartHandler(emitterAgent, 0,
                new NodeIdBitVectorReadOnly(new byte[]{1}), 0, clusterTopology);

            long beforeTopologyChangeTimestamp = long.MaxValue;
            var changeTopologyTask = Task.Run(() =>
            {
                Thread.Sleep(250);

                List<ClusterTopologyEntry> newTopology = new();
                newTopology.Add(new(){NodeId = 1, NodeRole = NodeRole.Emitter, RenderNodeId = 0});
                // 0 is not present in the list of entries anymore, so it is not an emitter anymore.

                beforeTopologyChangeTimestamp = Stopwatch.GetTimestamp();
                clusterTopology.Entries = newTopology;
            });

            var stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(0, m_MaxTestTime);
            long tryWaitEndTime = Stopwatch.GetTimestamp();
            Assert.DoesNotThrow(changeTopologyTask.Wait);
            Assert.That(stillWaitingOn, Is.Not.Null);
            Assert.That(stillWaitingOn.SetBitsCount, Is.EqualTo(1));
            Assert.That(stillWaitingOn[1], Is.True); // Should still be waiting on 1 event if it is now an emitter
            Assert.That(beforeTopologyChangeTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
            Assert.That(tryWaitEndTime - beforeTopologyChangeTimestamp, Is.LessThan(Stopwatch.Frequency * 2));
        }

        static void SendRepeaterWaiting(ulong frameIndex, byte repeaterId, bool willWaitNextFrame, IUdpAgent repeaterAgent)
        {
            repeaterAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame, new RepeaterWaitingToStartFrame()
            {
                FrameIndex = frameIndex,
                NodeId = repeaterId,
                WillUseNetworkSyncOnNextFrame = willWaitNextFrame
            });
        }

        static void TestEmitterWaitingMessage(ReceivedMessageBase receivedMessage, ulong frameIndex, byte[] stillWaitingNodes)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.EmitterWaitingToStartFrame));
            var receivedEmitterWaiting = receivedMessage as ReceivedMessage<EmitterWaitingToStartFrame>;
            Assert.That(receivedEmitterWaiting, Is.Not.Null);
            Assert.That(receivedEmitterWaiting.Payload.FrameIndex, Is.EqualTo(frameIndex));
            var compareArray = new ulong[4];
            (new NodeIdBitVectorReadOnly(stillWaitingNodes)).CopyTo(compareArray);
            var payload = receivedEmitterWaiting.Payload;
            Assert.That(payload.WaitingOn0, Is.EqualTo(compareArray[0]));
            Assert.That(payload.WaitingOn1, Is.EqualTo(compareArray[1]));
            Assert.That(payload.WaitingOn2, Is.EqualTo(compareArray[2]));
            Assert.That(payload.WaitingOn3, Is.EqualTo(compareArray[3]));
        }

        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
    }
}
