using System;
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
            var testNetwork = new TestUDPAgentNetwork();
            var emitterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, new NodeIdBitVectorReadOnly(new byte[]{1,3}));

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
            var testNetwork = new TestUDPAgentNetwork();
            var emitterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, new NodeIdBitVectorReadOnly(new byte[]{1,3,9}));

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
            var testNetwork = new TestUDPAgentNetwork();
            var emitterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, new NodeIdBitVectorReadOnly(new byte[]{2,7}));

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
            Assert.DoesNotThrow(repeaterTask1.Wait);
            Assert.That(stillWaitingOn, Is.Null);

            ret = waitingToStartHandler.PrepareForNextFrame(2);
            Assert.That(ret, Is.False);

            stillWaitingOn = waitingToStartHandler.TryWaitForAllRepeatersReady(2, m_MaxTestTime);
            Assert.That(stillWaitingOn, Is.Null);
        }

        [Test]
        public void MessagesFromWrongFrameIgnored()
        {
            var testNetwork = new TestUDPAgentNetwork();
            var emitterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.RepeaterWaitingToStartFrame});
            var repeaterAgent = new TestUDPAgent(testNetwork, new[] {MessageType.EmitterWaitingToStartFrame});
            using var waitingToStartHandler =
                new FrameWaitingToStartHandler(emitterAgent, new NodeIdBitVectorReadOnly(new byte[]{2,7}));

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
            Assert.DoesNotThrow(repeaterTask1.Wait);
            Assert.That(stillWaitingOn, Is.Null);
            Assert.That(beforeSendingRightFramesTimestamp, Is.LessThanOrEqualTo(tryWaitEndTime));
        }

        static void SendRepeaterWaiting(ulong frameIndex, byte repeaterId, bool willWaitNextFrame, IUDPAgent repeaterAgent)
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
            unsafe
            {
                var payload = receivedEmitterWaiting.Payload;
                Assert.That(payload.WaitingOn[0], Is.EqualTo(compareArray[0]));
                Assert.That(payload.WaitingOn[1], Is.EqualTo(compareArray[1]));
                Assert.That(payload.WaitingOn[2], Is.EqualTo(compareArray[2]));
                Assert.That(payload.WaitingOn[3], Is.EqualTo(compareArray[3]));
            }
        }

        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
    }
}
