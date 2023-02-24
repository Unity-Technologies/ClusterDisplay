using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using Unity.Collections;
using Debug = UnityEngine.Debug;
using Random = System.Random;
using static Unity.ClusterDisplay.Tests.Utilities;

namespace Unity.ClusterDisplay.Tests
{
    public class FrameDataAssemblerTests
    {
        [Test]
        public void SingleMessageFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 0, sendAgent);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void SingleMessageFrameDataThroughFirstFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});

            int messageLength = 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            var frameDataFirstPart = FakeReceivedData(42, toTransmit, 0);

            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId, 0,
                    frameDataFirstPart) {FrameCompletionDelay = m_DisabledFrameCompletionDelay};
            frameDataFirstPart.Dispose();

            // Ideally we would like to have the assembled frame available in recvAgent, however since the received
            // message is a queue and their can be a lot of things already in that queue FrameDataAssembler' constructor
            // cannot queue the assembled FrameData at the right position.
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(0));

            // However the first FrameData of the next frame will trigger output
            int message2Length = 2000;
            var toTransmit2 = AllocRandomByteArray(message2Length);
            SendDatagram(43, toTransmit2, 0, sendAgent);

            // And now we should get frame 42.
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void TwoMessagesFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void DuplicateTransmission()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);

            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void TwoMultiMessagesFrames()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Send frame 42
            int frame42Length = k_DataPerMessage + 500;
            var toTransmit1 = AllocRandomByteArray(frame42Length);

            SendDatagram(42, toTransmit1, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame42DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame42DataMessage, 42, toTransmit1);

            // Repeat last message of frame 42, it should simply be ignored
            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            // Send next frame
            int frame43Length = k_DataPerMessage * 2 + 200;
            var toTransmit2 = AllocRandomByteArray(frame43Length);

            SendDatagram(43, toTransmit2, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 2, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame43DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame43DataMessage, 43, toTransmit2);
        }

        [Test]
        public void IgnoreMessageOfFutureFrame()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);

            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(44, toTransmit, 0, sendAgent); // 44 is not in the future by much but it's enough to be considered in the future
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void TwoMessagesSwappedFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var retransmitMessage = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmit(retransmitMessage, 42, 0, 1);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void LongFrameWithMultipleGaps()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            int messageLength = 9 * k_DataPerMessage + 100;
            var toTransmit = AllocRandomByteArray(messageLength);

            // Skipping datagram 0 and 1

            SendDatagram(42, toTransmit, 2, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using (var retransmit01Message = sendAgent.ConsumeNextReceivedMessage())
            {
                TestRetransmit(retransmit01Message, 42, 0, 2);
            }

            SendDatagram(42, toTransmit, 3, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            // Skipping datagram 4

            SendDatagram(42, toTransmit, 5, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using (var retransmit4Message = sendAgent.ConsumeNextReceivedMessage())
            {
                TestRetransmit(retransmit4Message, 42, 4, 5);
            }

            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 4, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            // Skipping datagram 6, 7 and 8

            SendDatagram(42, toTransmit, 9, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using (var retransmit678Message = sendAgent.ConsumeNextReceivedMessage())
            {
                TestRetransmit(retransmit678Message, 42, 6, 9);
            }

            SendDatagram(42, toTransmit, 7, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 0, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit, 8, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit, 6, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void MissingPacketsOverTwoFrames()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Start frame 42
            int frame42Length = k_DataPerMessage + 500;
            var toTransmit1 = AllocRandomByteArray(frame42Length);

            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var retransmitMessage = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmit(retransmitMessage, 42, 0, 1);

            // Send next frame
            int frame43Length = k_DataPerMessage * 2 + 200;
            var toTransmit2 = AllocRandomByteArray(frame43Length);

            SendDatagram(43, toTransmit2, 0, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 2, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            // Complete frame 42.  Ideally it should complete both frames, but because of the IUdpAgent.OnMessagePreProcess
            // mechanic on which the FrameDataAssembler is based, on received datagram can only produce one FrameData, so
            // we will have to push some "dummy" datagram to trigger the second one to be produced
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit1, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame42DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame42DataMessage, 42, toTransmit1);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame43DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame43DataMessage, 43, toTransmit2);
        }

        [Test]
        public void IgnoreGapsIfOrderedReceptionIsFalse()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, false, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Start frame 42
            int frame42Length = k_DataPerMessage + 500;
            var toTransmit1 = AllocRandomByteArray(frame42Length);

            SendDatagram(42, toTransmit1, 1, sendAgent);

            // Send next frame
            int frame43Length = k_DataPerMessage * 2 + 200;
            var toTransmit2 = AllocRandomByteArray(frame43Length);

            SendDatagram(43, toTransmit2, 0, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 2, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.Zero);

            // Complete frame 42.  Ideally it should complete both frames, but because of the IUdpAgent.OnMessagePreProcess
            // mechanic on which the FrameDataAssembler is based, on received datagram can only produce one FrameData, so
            // we will have to push some "dummy" datagram to trigger the second one to be produced
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit1, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame42DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame42DataMessage, 42, toTransmit1);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);
            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame43DataMessage = recvAgent.ConsumeNextReceivedMessage();
            TestFrameData(frame43DataMessage, 43, toTransmit2);
        }

        [Test]
        public void BruteForceTesting()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Since it is hard to think about all possible permutations, let's do some brute force testing where
            // datagrams are skipped.  Test is done with two "live" frames (to correctly test the support for completing
            // "one frame late" in FrameDataAssembler).
            Dictionary<ulong, byte[]> framesExpectedData = new();
            Queue<(ulong, int)> waitingTransmission = new();
            int randomSeed = Guid.NewGuid().GetHashCode();
            Debug.Log($"BruteForceTesting random seed: {randomSeed}");
            Random rnd = new Random(randomSeed);

            void QueueFrame(ulong frameIndex)
            {
                int frameDataSize = rnd.Next(100, 1024 * 1024); // 1 MB frame at 60 hz = 500 mbps!  So this is an heavy test
                framesExpectedData[frameIndex] = AllocRandomByteArray(frameDataSize);
                int datagramCount = frameDataSize / k_DataPerMessage;
                if (datagramCount * k_DataPerMessage < frameDataSize)
                {
                    ++datagramCount;
                }

                List<int> datagrams = new(Enumerable.Range(0, datagramCount));

                if (datagramCount > 1)
                {
                    int datagramsToRemove = rnd.Next(0, datagramCount - 1);
                    for (int toRemoveCounter = 0; toRemoveCounter < datagramsToRemove; ++toRemoveCounter)
                    {
                        datagrams.Remove(rnd.Next(0, datagrams.Count - 2));
                    }
                }

                datagrams.ForEach(datagramIndex => waitingTransmission.Enqueue((frameIndex,datagramIndex)));
            }

            ulong lastReceived = ulong.MaxValue;
            ulong nextFrameIndex = 25;
            QueueFrame(nextFrameIndex++);
            QueueFrame(nextFrameIndex++);
            while (nextFrameIndex <= 50)
            {
                Assert.That(waitingTransmission, Is.Not.Empty);
                (ulong frameIndex, int datagramIndex) = waitingTransmission.Dequeue();
                byte[] frameData = framesExpectedData[frameIndex];
                SendDatagram(frameIndex, frameData, datagramIndex, sendAgent);

                if (sendAgent.ReceivedMessagesCount > 0)
                {
                    using var receivedMessage = sendAgent.ConsumeNextReceivedMessage();
                    Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RetransmitFrameData));
                    var receivedRetransmit = receivedMessage as ReceivedMessage<RetransmitFrameData>;
                    Assert.That(receivedRetransmit, Is.Not.Null);
                    for (datagramIndex = receivedRetransmit.Payload.DatagramIndexIndexStart;
                         datagramIndex < receivedRetransmit.Payload.DatagramIndexIndexEnd; ++datagramIndex)
                    {
                        waitingTransmission.Enqueue((receivedRetransmit.Payload.FrameIndex, datagramIndex));
                    }
                    break;
                }
                if (recvAgent.ReceivedMessagesCount > 0)
                {
                    using var receivedMessage = recvAgent.ConsumeNextReceivedMessage();
                    Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.FrameData));
                    var receivedFrameData = receivedMessage as ReceivedMessage<FrameData>;
                    Assert.That(receivedFrameData, Is.Not.Null);
                    if (lastReceived != ulong.MaxValue)
                    {
                        Assert.That(receivedFrameData.Payload.FrameIndex, Is.EqualTo(lastReceived + 1));
                    }
                    lastReceived = receivedFrameData.Payload.FrameIndex;
                    TestFrameData(receivedFrameData, lastReceived, framesExpectedData[lastReceived]);
                    QueueFrame(nextFrameIndex++);
                }
            }
        }

        [Test]
        public void AssembledExtraDataReused()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Send frame 42
            int frame42Length = k_DataPerMessage + 500;
            var toTransmit1 = AllocRandomByteArray(frame42Length);

            SendDatagram(42, toTransmit1, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(42, toTransmit1, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));

            IReceivedMessageData frame42ExtraData;
            using (var frame42DataMessage = recvAgent.ConsumeNextReceivedMessage())
            {
                frame42ExtraData = frame42DataMessage.ExtraData;
                TestFrameData(frame42DataMessage, 42, toTransmit1);
            }

            // Send next frame
            int frame43Length = k_DataPerMessage * 2 + 200;
            var toTransmit2 = AllocRandomByteArray(frame43Length);

            SendDatagram(43, toTransmit2, 0, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 1, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            SendDatagram(43, toTransmit2, 2, sendAgent);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using (var frame43DataMessage = recvAgent.ConsumeNextReceivedMessage())
            {
                Assert.That(frame43DataMessage.ExtraData, Is.SameAs(frame42ExtraData));
                TestFrameData(frame43DataMessage, 43, toTransmit2);
            }
        }

        [Test]
        public void WillNeedFrameThatWasNeverSent()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId);

            frameDataAssembler.WillNeedFrame(42);
            using var retransmitRequest = sendAgent.TryConsumeNextReceivedMessage(m_WaitTimeout);
            Assert.That(retransmitRequest, Is.Not.Null);
            TestRetransmit(retransmitRequest, 42, 0, int.MaxValue);

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 0, sendAgent);
            SendDatagram(42, toTransmit, 1, sendAgent);
            using var frameDataMessage = ConsumeUntilNextFrameData(recvAgent);
            Assert.That(frameDataMessage, Is.Not.Null);
            TestFrameData(frameDataMessage, 42, toTransmit);
        }

        [Test]
        public void WillNeedFrameTriggerRetransmitOnMissingEnd()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId);

            int messageLength = k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 0, sendAgent);

            // Wait for 25 times the FrameCompletionDelay, shouldn't make test too long and should be enough to detect
            // a problem in FrameDataAssembler where it starts to ask for retransmit even if WillNeedFrame was not called.
            var waitTime = frameDataAssembler.FrameCompletionDelay * 25;
            var earlyRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(earlyRetransmit, Is.Null);

            // Call WillNeedFrame which should trigger retransmit
            frameDataAssembler.WillNeedFrame(42);

            using var retransmitRequest = sendAgent.TryConsumeNextReceivedMessage(m_WaitTimeout);
            Assert.That(retransmitRequest, Is.Not.Null);
            TestRetransmit(retransmitRequest, 42, 1, int.MaxValue);

            SendDatagram(42, toTransmit, 1, sendAgent);

            using var frameDataMessage = ConsumeUntilNextFrameData(recvAgent);
            Assert.That(frameDataMessage, Is.Not.Null);
            TestFrameData(frameDataMessage, 42, toTransmit);

            // Wait to see if we wouldn't receive any other retransmit after we receive the re-assembled frame data.
            var lateRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(lateRetransmit, Is.Null);
        }

        [Test]
        public void WillNeedFrameSendsRetransmitUntilAllPartsAreReceivedWithoutLastPart()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId);

            int messageLength = 3 * k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 1, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var retransmitMessage = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmit(retransmitMessage, 42, 0, 1);

            // Wait for 25 times the FrameCompletionDelay, shouldn't make test too long and should be enough to detect
            // a problem in FrameDataAssembler where it starts to ask for retransmit even if WillNeedFrame was not called.
            var waitTime = frameDataAssembler.FrameCompletionDelay * 25;
            var earlyRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(earlyRetransmit, Is.Null);

            // Call WillNeedFrame which should trigger retransmit
            frameDataAssembler.WillNeedFrame(42);

            // Clear all received messages
            ConsumeAllReceivedMessages(sendAgent);

            // Check we are receiving all the expected retransmit
            ValidateStillReceivingRetransmitFor(sendAgent, 42, new[] {0, 2, 3}, 3, m_WaitTimeout);

            // Retransmit #0
            SendDatagram(42, toTransmit, 0, sendAgent);
            ConsumeAllReceivedMessages(sendAgent);
            ValidateStillReceivingRetransmitFor(sendAgent, 42, new[] {2, 3}, 3, m_WaitTimeout);

            // Retransmit #3
            SendDatagram(42, toTransmit, 3, sendAgent);
            ConsumeAllReceivedMessages(sendAgent);
            ValidateStillReceivingRetransmitFor(sendAgent, 42, new[] {2}, 3, m_WaitTimeout);

            // Retransmit #2
            SendDatagram(42, toTransmit, 2, sendAgent);

            // And we should now receive the FrameData
            using var frameDataMessage = ConsumeUntilNextFrameData(recvAgent);
            Assert.That(frameDataMessage, Is.Not.Null);
            TestFrameData(frameDataMessage, 42, toTransmit);

            // Wait to see if we wouldn't receive any other retransmit after we receive the re-assembled frame data.
            var lateRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(lateRetransmit, Is.Null);
        }

        [Test]
        public void WillNeedFrameSendsRetransmitUntilAllPartsAreReceivedWithLastPart()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId);

            int messageLength = 3 * k_DataPerMessage + 500;
            var toTransmit = AllocRandomByteArray(messageLength);
            SendDatagram(42, toTransmit, 1, sendAgent);
            SendDatagram(42, toTransmit, 3, sendAgent);
            Assert.That(sendAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var retransmit0Message = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmit(retransmit0Message, 42, 0, 1);
            using var retransmit2Message = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmit(retransmit2Message, 42, 2, 3);

            // Wait for 25 times the FrameCompletionDelay, shouldn't make test too long and should be enough to detect
            // a problem in FrameDataAssembler where it starts to ask for retransmit even if WillNeedFrame was not called.
            var waitTime = frameDataAssembler.FrameCompletionDelay * 25;
            var earlyRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(earlyRetransmit, Is.Null);

            // Call WillNeedFrame which should trigger retransmit
            frameDataAssembler.WillNeedFrame(42);

            // Clear all received messages
            ConsumeAllReceivedMessages(sendAgent);

            // Check we are receiving all the expected retransmit
            ValidateStillReceivingRetransmitFor(sendAgent, 42, new[] {0, 2}, 3, m_WaitTimeout);

            // Retransmit #0
            SendDatagram(42, toTransmit, 0, sendAgent);
            ConsumeAllReceivedMessages(sendAgent);
            ValidateStillReceivingRetransmitFor(sendAgent, 42, new[] {2}, 3, m_WaitTimeout);

            // Retransmit #2
            SendDatagram(42, toTransmit, 2, sendAgent);

            // And we should now receive the FrameData
            using var frameDataMessage = ConsumeUntilNextFrameData(recvAgent);
            Assert.That(frameDataMessage, Is.Not.Null);
            TestFrameData(frameDataMessage, 42, toTransmit);

            // Wait to see if we wouldn't receive any other retransmit after we receive the re-assembled frame data.
            var lateRetransmit = sendAgent.TryConsumeNextReceivedMessage(waitTime);
            Assert.That(lateRetransmit, Is.Null);
        }

        [Test]
        public void RetransmitReceivedFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmittedReceivedFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,
                new [] {MessageType.FrameData, MessageType.RetransmitReceivedFrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId, 2)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Send frame 42 to 44
            Dictionary<int, byte[]> sentFrameData = new();
            for (int frameIndex = 42; frameIndex <= 44; ++frameIndex)
            {
                // Send frame 42
                int messageLength = k_DataPerMessage + 500 + frameIndex;
                var frameData = AllocRandomByteArray(messageLength);
                sentFrameData[frameIndex] = frameData;
                SendDatagram((ulong)frameIndex, frameData, 0, sendAgent);
                SendDatagram((ulong)frameIndex, frameData, 1, sendAgent);

                // We should receive it
                using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
                TestFrameData(frameDataMessage, (ulong)frameIndex, frameData);
            }

            AskReceivedRetransmit(sendAgent, k_RepeaterNodeId, 42);
            using var frame42Retransmitted = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame42Retransmitted, 42, -1, 0, 0, null);

            AskReceivedRetransmit(sendAgent, k_RepeaterNodeId, 45);
            using var frame45Retransmitted = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame45Retransmitted, 45, -1, 0, 0, null);

            AskReceivedRetransmit(sendAgent, k_RepeaterNodeId, 43);
            using var frame43RetransmittedA = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame43RetransmittedA, 43, sentFrameData[43].Length, 0, 0,
                sentFrameData[43][0..k_DataPerMessage]);
            using var frame43RetransmittedB = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame43RetransmittedB, 43, sentFrameData[43].Length, 1, k_DataPerMessage,
                sentFrameData[43][k_DataPerMessage..]);

            AskReceivedRetransmit(sendAgent, k_RepeaterNodeId, 44);
            using var frame44RetransmittedA = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame44RetransmittedA, 44, sentFrameData[44].Length, 0, 0,
                sentFrameData[44][0..k_DataPerMessage]);
            using var frame44RetransmittedB = sendAgent.ConsumeNextReceivedMessage();
            TestRetransmittedFrameData(frame44RetransmittedB, 44, sentFrameData[44].Length, 1, k_DataPerMessage,
                sentFrameData[44][k_DataPerMessage..]);
        }

        [Test]
        public void RetransmitReceivedFrameDataWhenNotActive()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmittedReceivedFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,
                new [] {MessageType.FrameData, MessageType.RetransmitReceivedFrameData});
            using var frameDataAssembler = new FrameDataAssembler(recvAgent, true, k_RepeaterNodeId, 0)
                {FrameCompletionDelay = m_DisabledFrameCompletionDelay};

            // Send frame 42 to 44
            Dictionary<int, byte[]> sentFrameData = new();
            for (int frameIndex = 42; frameIndex <= 44; ++frameIndex)
            {
                // Send frame 42
                int messageLength = k_DataPerMessage + 500 + frameIndex;
                var frameData = AllocRandomByteArray(messageLength);
                sentFrameData[frameIndex] = frameData;
                SendDatagram((ulong)frameIndex, frameData, 0, sendAgent);
                SendDatagram((ulong)frameIndex, frameData, 1, sendAgent);

                // We should receive it
                using var frameDataMessage = recvAgent.ConsumeNextReceivedMessage();
                TestFrameData(frameDataMessage, (ulong)frameIndex, frameData);
            }

            for (int frameIndex = 42; frameIndex <= 44; ++frameIndex)
            {
                AskReceivedRetransmit(sendAgent, k_RepeaterNodeId, (ulong)frameIndex);
                using var retransmittedAnswer = sendAgent.ConsumeNextReceivedMessage();
                TestRetransmittedFrameData(retransmittedAnswer, (ulong)frameIndex, -1, 0, 0, null);
            }
        }

        void SendDatagram(ulong frameIndex, byte[] data, int datagramIndex, IUdpAgent udpAgent)
        {
            var frameData = new FrameData()
            {
                FrameIndex = frameIndex,
                DataLength = data.Length,
                DatagramIndex = datagramIndex,
                DatagramDataOffset = datagramIndex * k_DataPerMessage
            };
            int length = Math.Min(data.Length - frameData.DatagramDataOffset, k_DataPerMessage);
            var extraData = new NativeArray<byte>(length, Allocator.Temp);
            NativeArray<byte>.Copy(data, frameData.DatagramDataOffset, extraData, 0, length);
            udpAgent.SendMessage(MessageType.FrameData, frameData, extraData.AsReadOnly());
        }

        ReceivedMessage<FrameData> FakeReceivedData(ulong frameIndex, byte[] data, int datagramIndex)
        {
            var ret = ReceivedMessage<FrameData>.GetFromPool();
            ret.Payload = new FrameData()
            {
                FrameIndex = frameIndex,
                DataLength = data.Length,
                DatagramIndex = datagramIndex,
                DatagramDataOffset = datagramIndex * k_DataPerMessage
            };
            int length = Math.Min(data.Length - ret.Payload.DatagramDataOffset, k_DataPerMessage);
            var extraData = new NativeArray<byte>(length, Allocator.Temp);
            NativeArray<byte>.Copy(data, ret.Payload.DatagramDataOffset, extraData, 0, length);
            ret.AdoptExtraData(new TestUdpAgentNativeExtraData(extraData.AsReadOnly()));
            return ret;
        }

        static void TestFrameData(ReceivedMessageBase receivedMessage, ulong frameIndex, byte[] data)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.FrameData));
            var receivedFrameData = receivedMessage as ReceivedMessage<FrameData>;
            Assert.That(receivedFrameData, Is.Not.Null);

            Assert.That(receivedFrameData.Payload.FrameIndex, Is.EqualTo(frameIndex));
            Assert.That(receivedFrameData.Payload.DataLength, Is.EqualTo(data.Length));
            Assert.That(receivedFrameData.Payload.DatagramIndex, Is.Zero);
            Assert.That(receivedFrameData.Payload.DatagramDataOffset, Is.Zero);

            Assert.That(receivedFrameData.ExtraData.Length, Is.EqualTo(data.Length));
            receivedFrameData.ExtraData.AsManagedArray(out var bytes, out var extraDataStart, out var extraDataLength);
            Assert.That(new Span<byte>(bytes, extraDataStart, extraDataLength).ToArray(), Is.EqualTo(data));
        }

        static void TestRetransmit(ReceivedMessageBase receivedMessage, ulong frameIndex, int datagramIndexStart, int datagramIndexEnd)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RetransmitFrameData));
            var receivedRetransmit = receivedMessage as ReceivedMessage<RetransmitFrameData>;
            Assert.That(receivedRetransmit, Is.Not.Null);

            Assert.That(receivedRetransmit.Payload.FrameIndex, Is.EqualTo(frameIndex));
            Assert.That(receivedRetransmit.Payload.DatagramIndexIndexStart, Is.EqualTo(datagramIndexStart));
            Assert.That(receivedRetransmit.Payload.DatagramIndexIndexEnd, Is.EqualTo(datagramIndexEnd));
        }

        static ReceivedMessageBase ConsumeUntilNextFrameData(IUdpAgent udpAgent)
        {
            for (;;)
            {
                var receivedMessage = udpAgent.TryConsumeNextReceivedMessage();
                if (receivedMessage != null)
                {
                    if (receivedMessage.Type == MessageType.FrameData)
                    {
                        return receivedMessage;
                    }
                    receivedMessage.Dispose();
                }
                else
                {
                    return null;
                }
            }
        }

        static void ConsumeAllReceivedMessages(IUdpAgent udpAgent)
        {
            for (;;)
            {
                var receivedMessage = udpAgent.TryConsumeNextReceivedMessage();
                if (receivedMessage != null)
                {
                    receivedMessage.Dispose();
                }
                else
                {
                    break;
                }
            }
        }

        static void ValidateStillReceivingRetransmitFor(IUdpAgent udpAgent, ulong frameIndex, int[] datagramIndices,
            int maxDatagramIndex, TimeSpan timeout)
        {
            var expectedRetransmit = new BitArray(datagramIndices.Max() + 1);
            var missingRetransmit = new BitArray(datagramIndices.Max() + 1);
            int missingRetransmitCount = 0;
            foreach (var datagramIndex in datagramIndices)
            {
                expectedRetransmit[datagramIndex] = true;
                if (!missingRetransmit[datagramIndex])
                {
                    missingRetransmit[datagramIndex] = true;
                    ++missingRetransmitCount;
                }
            }

            var validateStartTime = new Stopwatch();
            validateStartTime.Start();
            while (validateStartTime.Elapsed < timeout && missingRetransmitCount > 0)
            {
                using var message = udpAgent.TryConsumeNextReceivedMessage(timeout - validateStartTime.Elapsed);
                if (message != null)
                {
                    Assert.That(message.Type, Is.EqualTo(MessageType.RetransmitFrameData));
                    var retransmitMessage = message as ReceivedMessage<RetransmitFrameData>;
                    Assert.That(retransmitMessage, Is.Not.Null);
                    if (retransmitMessage.Payload.FrameIndex == frameIndex)
                    {
                        for (int datagramIndex = Math.Max(0, retransmitMessage.Payload.DatagramIndexIndexStart);
                             datagramIndex < Math.Min(maxDatagramIndex + 1, retransmitMessage.Payload.DatagramIndexIndexEnd);
                             ++datagramIndex)
                        {
                            Assert.That(expectedRetransmit[datagramIndex], Is.True);
                            if (missingRetransmit[datagramIndex])
                            {
                                missingRetransmit[datagramIndex] = false;
                                --missingRetransmitCount;
                            }
                        }
                    }
                }
            }

            Assert.That(missingRetransmitCount, Is.Zero);
        }

        static void AskReceivedRetransmit(IUdpAgent udpAgent, byte nodeId, ulong frameIndex)
        {
            udpAgent.SendMessage(MessageType.RetransmitReceivedFrameData,
                new RetransmitReceivedFrameData() {NodeId = nodeId, FrameIndex = frameIndex});
        }

        static void TestRetransmittedFrameData(ReceivedMessageBase receivedMessage, ulong frameIndex, int fullDataLength,
            int datagramIndex, int datagramOffset, [CanBeNull] byte[] datagramData)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RetransmittedReceivedFrameData));
            var receivedFrameData = receivedMessage as ReceivedMessage<RetransmittedReceivedFrameData>;
            Assert.That(receivedFrameData, Is.Not.Null);

            Assert.That(receivedFrameData.Payload.FrameIndex, Is.EqualTo(frameIndex));
            Assert.That(receivedFrameData.Payload.DataLength, Is.EqualTo(fullDataLength));
            Assert.That(receivedFrameData.Payload.DatagramIndex, Is.EqualTo(datagramIndex));
            Assert.That(receivedFrameData.Payload.DatagramDataOffset, Is.EqualTo(datagramOffset));

            if (datagramData != null)
            {
                Assert.That(receivedFrameData.ExtraData.Length, Is.EqualTo(datagramData.Length));
                receivedFrameData.ExtraData.AsManagedArray(out var bytes, out var extraDataStart, out var extraDataLength);
                Assert.That(new Span<byte>(bytes, extraDataStart, extraDataLength).ToArray(), Is.EqualTo(datagramData));
            }
        }

        const byte k_RepeaterNodeId = 28;
        const int k_DataPerMessage = 1237;
        TimeSpan m_DisabledFrameCompletionDelay = TimeSpan.FromHours(24);
        TimeSpan m_WaitTimeout = TimeSpan.FromSeconds(5);
    }
}
