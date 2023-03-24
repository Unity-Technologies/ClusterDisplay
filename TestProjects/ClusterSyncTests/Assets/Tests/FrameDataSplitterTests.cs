using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.Utilities;

namespace Unity.ClusterDisplay.Tests
{
    public class FrameDataSplitterTests
    {
        [Test]
        public void PassThroughSmallFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataSplitter = new FrameDataSplitter(sendAgent);

            const int frameDataLength = 500;
            Assert.That(frameDataLength, Is.LessThan(sendAgent.MaximumMessageSize));
            var frame42Data = AllocateRandomFrameDataBuffer(frameDataLength);
            var frame42DataPayload = frame42Data.Data().ToArray();
            frameDataSplitter.SendFrameData(42, ref frame42Data);
            Assert.That(frame42Data, Is.Null);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var receivedMessage = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(receivedMessage, 42, frameDataLength, 0, 0, frame42DataPayload);
        }

        [Test]
        public void SplitFrameData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataSplitter = new FrameDataSplitter(sendAgent);

            // All our message sizes of this test are based on that, will need to update the test if it changes...
            Assert.That(sendAgent.MaximumMessageSize, Is.EqualTo(1257));
            const int frameDataLength = 2000;
            var frame42Data = AllocateRandomFrameDataBuffer(frameDataLength);
            var frame42DataPayload = frame42Data.Data().ToArray();
            frameDataSplitter.SendFrameData(42, ref frame42Data);
            Assert.That(frame42Data, Is.Null);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var receivedMessage1 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(receivedMessage1, 42, frameDataLength, 0, 0, frame42DataPayload[..1237]);
            using var receivedMessage2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(receivedMessage2, 42, frameDataLength, 1, 1237, frame42DataPayload[1237..]);
        }

        [Test]
        public void RetransmitData()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataSplitter = new FrameDataSplitter(sendAgent, true);

            // 1. Before testing re-transmit we need to first transmit something!
            // While at it, test that what we send to the repeaters is the expected content.

            // All our message sizes of this test are based on that, will need to update the test if it changes...
            Assert.That(sendAgent.MaximumMessageSize, Is.EqualTo(1257));
            const int dataPerMessage = 1237;

            // Transmit frame 41
            const int frame41DataLength = 500;
            var frame41Data = AllocateRandomFrameDataBuffer(frame41DataLength);
            var frame41DataPayload = frame41Data.Data().ToArray();
            frameDataSplitter.SendFrameData(41, ref frame41Data);
            Assert.That(frame41Data, Is.Null);

            // Check that we receive what we expect
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            using var frame41Message0 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame41Message0, 41, frame41DataLength, 0, 0, frame41DataPayload);

            // Transmit frame 42
            const int frame42DataLength = 3000;
            var frame42Data = AllocateRandomFrameDataBuffer(frame42DataLength);
            var frame42DataPayload = frame42Data.Data().ToArray();
            frameDataSplitter.SendFrameData(42, ref frame42Data);
            Assert.That(frame42Data, Is.Null);

            // Check that we receive what we expect
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(3));
            using var frame42Message0 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame42Message0, 42, frame42DataLength, 0, 0,
                frame42DataPayload[..dataPerMessage]);
            using var frame42Message1 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame42Message1, 42, frame42DataLength, 1, dataPerMessage,
                frame42DataPayload[dataPerMessage..(dataPerMessage * 2)]);
            using var frame42Message2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame42Message2, 42, frame42DataLength, 2, dataPerMessage * 2,
                frame42DataPayload[(dataPerMessage * 2)..]);

            // So far, frameDataSplitter should still use every FrameData it has been pushed, so nothing should be
            // returned to the object pool.
            Assert.That(frameDataSplitter.FrameDataBufferPool.CountInactive, Is.Zero);

            // Transmit frame 43
            const int frame43DataLength = 4000;
            var frame43Data = AllocateRandomFrameDataBuffer(frame43DataLength);
            var frame43DataPayload = frame43Data.Data().ToArray();
            frameDataSplitter.SendFrameData(43, ref frame43Data);
            Assert.That(frame43Data, Is.Null);
            Assert.That(frameDataSplitter.FrameDataBufferPool.CountInactive, Is.EqualTo(1));

            // Check that we receive what we expect
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(4));
            using var frame43Message0 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame43Message0, 43, frame43DataLength, 0, 0,
                frame43DataPayload[..dataPerMessage]);
            using var frame43Message1 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame43Message1, 43, frame43DataLength, 1, dataPerMessage,
                frame43DataPayload[dataPerMessage..(dataPerMessage * 2)]);
            using var frame43Message2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame43Message2, 43, frame43DataLength, 2, dataPerMessage * 2,
                frame43DataPayload[(dataPerMessage * 2)..(dataPerMessage * 3)]);
            using var frame43Message3 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame43Message3, 43, frame43DataLength, 3, dataPerMessage * 3,
                frame43DataPayload[(dataPerMessage * 3)..]);

            // 2. Now we can start testing re-transmit

            // Ask for a frame that is not in the retransmit cache anymore
            SendReTransmit(recvAgent, 41, 0, 1);
            LogAssert.Expect(LogType.Warning,"Asking to retransmit a frame for which we currently do not have data anymore: 41, we only have frames in the range of [42, 43], skipping.");
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            // Ask for the first datagram of frame 42
            SendReTransmit(recvAgent, 42, 0, 1);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(1));
            CompareNextRetransmitted(recvAgent, frame42Message0);

            // Small sleep so that the mechanism to avoid repeated retransmissions in FrameDataSplitter does not kick in
            // and avoid retransmitting datagram 0.
            Thread.Sleep(k_RetransmitDetectionProtectionMs);

            // Ask for all the datagrams by asking for a range starting before the first and ending after the last,
            // retransmission logic should clamp everything without any problem.
            SendReTransmit(recvAgent, 42, -1, 666);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(3));
            CompareNextRetransmitted(recvAgent, frame42Message0);
            CompareNextRetransmitted(recvAgent, frame42Message1);
            CompareNextRetransmitted(recvAgent, frame42Message2);

            // Ask for some packets in the middle of 43
            SendReTransmit(recvAgent, 43, 1, 3);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            CompareNextRetransmitted(recvAgent, frame43Message1);
            CompareNextRetransmitted(recvAgent, frame43Message2);

            // Ask for packets of a frame that was not sent yet, this will not produce warning since it might be normal
            // when repeater is faster than emitter...
            SendReTransmit(recvAgent, 44, 0, 1);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.Zero);

            // Small sleep so that the mechanism to avoid repeated retransmissions in FrameDataSplitter does not kick in
            // and avoid retransmitting anything.
            Thread.Sleep(k_RetransmitDetectionProtectionMs);

            // Ask back from frame 42 (to be sure asking for stuff after does not flush what we already have in cache)
            SendReTransmit(recvAgent, 42, 1, 3);
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            CompareNextRetransmitted(recvAgent, frame42Message1);
            CompareNextRetransmitted(recvAgent, frame42Message2);
        }

        [Test]
        public void RetransmitFrame0Data()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataSplitter = new FrameDataSplitter(sendAgent);

            // All our message sizes of this test are based on that, will need to update the test if it changes...
            Assert.That(sendAgent.MaximumMessageSize, Is.EqualTo(1257));
            int dataPerMessage = 1237;

            // 1. Transmit frame 0

            int frame0DataLength = 2000;
            var frame0Data = AllocateRandomFrameDataBuffer(frame0DataLength);
            var frame0DataPayload = frame0Data.Data().ToArray();
            frameDataSplitter.SendFrameData(0, ref frame0Data);
            Assert.That(frame0Data, Is.Null);

            // Check that we received what we expect
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var frame0Message0 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message0, 0, frame0DataLength, 0, 0,
                frame0DataPayload[..dataPerMessage]);
            using var frame0Message1 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message1, 0, frame0DataLength, 1, dataPerMessage,
                frame0DataPayload[dataPerMessage..]);

            // 2. Ask to retransmit datagrams of frame 0
            Thread.Sleep(k_RetransmitDetectionProtectionMs);
            SendReTransmit(recvAgent, 0, 0, Int32.MaxValue);

            // Check everything got retransmitted
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var frame0Message0Take2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message0Take2, 0, frame0DataLength, 0, 0,
                frame0DataPayload[..dataPerMessage]);
            using var frame0Message1Take2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message1Take2, 0, frame0DataLength, 1, dataPerMessage,
                frame0DataPayload[dataPerMessage..]);

            // 3. Transmit frame 1
            int frame1DataLength = 3000;
            var frame1Data = AllocateRandomFrameDataBuffer(frame1DataLength);
            var frame1DataPayload = frame1Data.Data().ToArray();
            frameDataSplitter.SendFrameData(1, ref frame1Data);
            Assert.That(frame1Data, Is.Null);

            // Check that we received what we expect
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(3));
            using var frame1Message0 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame1Message0, 1, frame1DataLength, 0, 0,
                frame1DataPayload[..dataPerMessage]);
            using var frame1Message1 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame1Message1, 1, frame1DataLength, 1, dataPerMessage,
                frame1DataPayload[dataPerMessage..(dataPerMessage * 2)]);
            using var frame1Message2 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame1Message2, 1, frame1DataLength, 2, 2 * dataPerMessage,
                frame1DataPayload[(dataPerMessage * 2)..]);

            // 4. Ask to retransmit datagram frame 0 (again)
            Thread.Sleep(k_RetransmitDetectionProtectionMs);
            SendReTransmit(recvAgent, 0, 0, Int32.MaxValue);

            // Check everything got retransmitted
            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var frame0Message0Take3 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message0Take3, 0, frame0DataLength, 0, 0,
                frame0DataPayload[..dataPerMessage]);
            using var frame0Message1Take3 = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame0Message1Take3, 0, frame0DataLength, 1, dataPerMessage,
                frame0DataPayload[dataPerMessage..]);
        }

        [Test]
        public void SkipFrames()
        {
            var testNetwork = new TestUdpAgentNetwork();
            var sendAgent = new TestUdpAgent(testNetwork, new [] {MessageType.RetransmitFrameData});
            var recvAgent = new TestUdpAgent(testNetwork,new [] {MessageType.FrameData});
            using var frameDataSplitter = new FrameDataSplitter(sendAgent);

            int frameDataLength = 500;
            var frameData1 = AllocateRandomFrameDataBuffer(frameDataLength);
            var frameData1Payload = frameData1.Data().ToArray();
            var frameData2 = AllocateRandomFrameDataBuffer(frameDataLength);
            var frameData2Payload = frameData2.Data().ToArray();

            frameDataSplitter.SendFrameData(42, ref frameData1);
            Assert.That(frameData1, Is.Null);
            Assert.Throws<ArgumentException>(() => frameDataSplitter.SendFrameData(44, ref frameData2));
            Assert.That(frameData2, Is.Not.Null);
            Assert.Throws<ArgumentException>(() => frameDataSplitter.SendFrameData(41, ref frameData2));
            Assert.That(frameData2, Is.Not.Null);
            frameDataSplitter.SendFrameData(43, ref frameData2);
            Assert.That(frameData2, Is.Null);

            Assert.That(recvAgent.ReceivedMessagesCount, Is.EqualTo(2));
            using var frame42Message = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame42Message, 42, frameDataLength, 0, 0, frameData1Payload);
            using var frame43Message = recvAgent.ConsumeNextReceivedMessage();
            TestReceivedFrameData(frame43Message, 43, frameDataLength, 0, 0, frameData2Payload);
        }

        static FrameDataBuffer AllocateRandomFrameDataBuffer(int length)
        {
            var ret = new FrameDataBuffer();
            int frameDataBufferOverhead = 8; // A int for the id and another int for the length of each data
            Assert.That(length, Is.GreaterThan(frameDataBufferOverhead));
            int effectiveLength = length - frameDataBufferOverhead;
            ret.Store(0, nativeArray =>
            {
                NativeArray<byte>.Copy(AllocRandomByteArray(effectiveLength), 0, nativeArray, 0, effectiveLength);
                return effectiveLength;
            });
            return ret;
        }

        static void TestReceivedFrameData(ReceivedMessageBase receivedMessage, ulong frameIndex, int frameDataLength,
            int datagramIndex, int datagramDataOffset, byte[] extraData)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.FrameData));
            Assert.That(receivedMessage.ExtraData.ToArray(), Is.EqualTo(extraData));
            var receivedFrameDataMessage = receivedMessage as ReceivedMessage<FrameData>;
            Assert.That(receivedFrameDataMessage, Is.Not.Null);
            Assert.That(receivedFrameDataMessage.Payload.FrameIndex, Is.EqualTo(frameIndex));
            Assert.That(receivedFrameDataMessage.Payload.DataLength, Is.EqualTo(frameDataLength));
            Assert.That(receivedFrameDataMessage.Payload.DatagramIndex, Is.EqualTo(datagramIndex));
            Assert.That(receivedFrameDataMessage.Payload.DatagramDataOffset, Is.EqualTo(datagramDataOffset));
        }

        static void SendReTransmit(IUdpAgent udpAgent, ulong frameIndex, int datagramIndexIndexStart,
            int datagramIndexIndexEnd)
        {
            var message = new RetransmitFrameData()
            {
                FrameIndex = frameIndex,
                DatagramIndexIndexStart = datagramIndexIndexStart,
                DatagramIndexIndexEnd = datagramIndexIndexEnd
            };
            udpAgent.SendMessage(MessageType.RetransmitFrameData, message);
        }

        static void CompareNextRetransmitted(IUdpAgent udpAgent, ReceivedMessageBase compareTo)
        {
            using var retransmitted = udpAgent.ConsumeNextReceivedMessage();
            var compareToFrameData = compareTo as ReceivedMessage<FrameData>;
            Assert.That(compareToFrameData, Is.Not.Null);
            var compareExtraData = compareTo.ExtraData.AsNativeArray();

            TestReceivedFrameData(retransmitted, compareToFrameData.Payload.FrameIndex,
                compareToFrameData.Payload.DataLength, compareToFrameData.Payload.DatagramIndex,
                compareToFrameData.Payload.DatagramDataOffset, compareExtraData.ToArray());
        }

        const int k_RetransmitDetectionProtectionMs = 5;
    }
}
