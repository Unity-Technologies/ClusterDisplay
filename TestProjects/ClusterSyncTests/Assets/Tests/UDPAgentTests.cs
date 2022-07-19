using System;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.ClusterDisplay.Tests
{
    public class UdpAgentTests
    {
        [Test]
        public void Dispose()
        {
            using var agent = new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));
            Thread.Sleep(100); // A small wait to give the time to threads inside UdpAgent to correctly start
        }

        [Test]
        public void SendReceive()
        {
            using var sender =   new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));

            // Send
            RegisteringWithEmitter messageToSend;
            messageToSend.NodeId = 42;
            messageToSend.IPAddressBytes = BitConverter.ToUInt32(sender.AdapterAddress.GetAddressBytes());
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);

            // This is how we will validate what we receive
            void TestReceivedMessage( ReceivedMessageBase msg )
            {
                Assert.That(msg, Is.Not.Null);
                Assert.That(msg.Type, Is.EqualTo(MessageType.RegisteringWithEmitter));
                Assert.That(msg.ExtraData, Is.Null);
                var msgOfType = msg as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(msgOfType, Is.Not.Null);
                Assert.That(msgOfType.Payload.NodeId, Is.EqualTo(messageToSend.NodeId));
                Assert.That(msgOfType.Payload.IPAddressBytes, Is.EqualTo(messageToSend.IPAddressBytes));
            }

            // Receive
            using var messageFromReceiver = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            TestReceivedMessage(messageFromReceiver);

            // In theory sender should also have received it (even if not that useful), so let's ensure it also received
            // it.
            using var messageFromSender = sender.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            TestReceivedMessage(messageFromSender);
        }

        [Test]
        public void ReceivesNothing()
        {
            using var sender =   new UdpAgent(GetConfig(null));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));

            // Send
            RegisteringWithEmitter messageToSend;
            messageToSend.NodeId = 42;
            messageToSend.IPAddressBytes = BitConverter.ToUInt32(sender.AdapterAddress.GetAddressBytes());
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);

            // Receive
            using var messageFromReceiver = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            Assert.That(messageFromReceiver, Is.Not.Null);

            // Sender shouldn't have received anything
            Assert.That(receiver.ReceivedMessagesCount, Is.Zero);
        }

        [Test]
        public void SendReceiveExtraData()
        {
            using var sender =   new UdpAgent(GetConfig(new [] {MessageType.FrameData}));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.FrameData}));

            // Prepare message and Send
            var messageToSend = new FrameData()
            {
                FrameIndex = 28,
                DataLength = 4000,
                DatagramIndex = 2,
                DatagramDataOffset = 2800
            };
            const int extraDataLength = 1200;
            var extraData = AllocRandomData(extraDataLength);
            using var extraDataNative = new NativeArray<byte>(extraData, Allocator.Temp);
            sender.SendMessage(MessageType.FrameData, messageToSend, extraDataNative.AsReadOnly());

            // This is how we will validate what we receive
            void TestReceivedMessage( ReceivedMessageBase msg )
            {
                Assert.That(msg, Is.Not.Null);
                Assert.That(msg.Type, Is.EqualTo(MessageType.FrameData));
                Assert.That(msg.ExtraData, Is.Not.Null);
                Assert.That(msg.ExtraData.Length, Is.EqualTo(extraDataLength));
                Assert.That(msg.ExtraData.ToArray(), Is.EqualTo(extraData));
                var msgOfType = msg as ReceivedMessage<FrameData>;
                Assert.That(msgOfType, Is.Not.Null);
                Assert.That(msgOfType.Payload.FrameIndex, Is.EqualTo(messageToSend.FrameIndex));
                Assert.That(msgOfType.Payload.DataLength, Is.EqualTo(messageToSend.DataLength));
                Assert.That(msgOfType.Payload.DatagramIndex, Is.EqualTo(messageToSend.DatagramIndex));
                Assert.That(msgOfType.Payload.DatagramDataOffset, Is.EqualTo(messageToSend.DatagramDataOffset));
            }

            // Receive
            using var messageFromReceiver = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            TestReceivedMessage(messageFromReceiver);

            // In theory sender should also have received it (even if not that useful), so let's ensure it also received
            // it.
            using var messageFromSender = sender.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            TestReceivedMessage(messageFromSender);
        }

        [Test]
        public void ReuseReceivedMessages()
        {
            byte GetReceivedMessageNodeId(ReceivedMessageBase receivedMessage)
            {
                var receivedMessageOfType = receivedMessage as ReceivedMessage<RegisteringWithEmitter>;
                Assert.That(receivedMessageOfType, Is.Not.Null);
                return receivedMessageOfType.Payload.NodeId;
            }

            using var sender = new UdpAgent(GetConfig(null));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));

            // Send
            var messageToSend = new RegisteringWithEmitter()
            {
                NodeId = 42,
                IPAddressBytes = BitConverter.ToUInt32(sender.AdapterAddress.GetAddressBytes())
            };
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);

            // Receive them
            var received1 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received2 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));

            Assert.That(received1, Is.Not.Null);
            Assert.That(GetReceivedMessageNodeId(received1), Is.EqualTo(42));
            Assert.That(received2, Is.Not.Null);
            Assert.That(GetReceivedMessageNodeId(received2), Is.EqualTo(42));

            // Dispose of received2 so we should get it back next time we receive a message
            received2.Dispose();

            // Send other messages
            messageToSend.NodeId = 28;
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);
            var received3 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received4 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));

            Assert.That(GetReceivedMessageNodeId(received1), Is.EqualTo(42));
            Assert.That(GetReceivedMessageNodeId(received2), Is.EqualTo(28));
            Assert.That(received3, Is.Not.Null);
            Assert.That(received3, Is.SameAs(received2));
            Assert.That(GetReceivedMessageNodeId(received3), Is.EqualTo(28));
            Assert.That(received4, Is.Not.Null);
            Assert.That(GetReceivedMessageNodeId(received4), Is.EqualTo(28));
        }

        [Test]
        public void ReuseReceivedMessagesWithExtraData()
        {
            ulong GetReceivedMessageFrameIndex(ReceivedMessageBase receivedMessage)
            {
                var receivedMessageOfType = receivedMessage as ReceivedMessage<FrameData>;
                Assert.That(receivedMessageOfType, Is.Not.Null);
                return receivedMessageOfType.Payload.FrameIndex;
            }

            using var sender = new UdpAgent(GetConfig(null));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.FrameData}));

            // Prepare message and Send
            var messageToSend = new FrameData()
            {
                FrameIndex = 42,
                DataLength = 1000,
                DatagramIndex = 0,
                DatagramDataOffset = 0
            };
            const int extraDataLength = 1000;
            var extraData1 = AllocRandomData(extraDataLength);
            using var extraData1Native = new NativeArray<byte>(extraData1, Allocator.Temp);
            sender.SendMessage(MessageType.FrameData, messageToSend, extraData1Native.AsReadOnly());

            messageToSend.FrameIndex = 43;
            var extraData2 = AllocRandomData(extraDataLength);
            using var extraData2Native = new NativeArray<byte>(extraData2, Allocator.Temp);
            sender.SendMessage(MessageType.FrameData, messageToSend, extraData2Native.AsReadOnly());

            // Receive them
            var received1 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received1ExtraData = received1.ExtraData;
            var received2 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received2ExtraData = received2.ExtraData;

            Assert.That(received1, Is.Not.Null);
            Assert.That(GetReceivedMessageFrameIndex(received1), Is.EqualTo(42));
            Assert.That(received1.ExtraData.ToArray(), Is.EqualTo(extraData1));
            Assert.That(received2, Is.Not.Null);
            Assert.That(GetReceivedMessageFrameIndex(received2), Is.EqualTo(43));
            Assert.That(received2.ExtraData.ToArray(), Is.EqualTo(extraData2));

            // Dispose of received2 so we should get it back next time we receive a message
            received2.Dispose();

            // Send other messages
            messageToSend.FrameIndex = 44;
            var extraData3 = AllocRandomData(extraDataLength);
            using var extraData3Native = new NativeArray<byte>(extraData3, Allocator.Temp);
            sender.SendMessage(MessageType.FrameData, messageToSend, extraData3Native.AsReadOnly());

            messageToSend.FrameIndex = 45;
            var extraData4 = AllocRandomData(extraDataLength);
            using var extraData4Native = new NativeArray<byte>(extraData4, Allocator.Temp);
            sender.SendMessage(MessageType.FrameData, messageToSend, extraData4Native.AsReadOnly());

            var received3 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received3ExtraData = received3.ExtraData;
            var received4 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received4ExtraData = received4.ExtraData;

            Assert.That(GetReceivedMessageFrameIndex(received1), Is.EqualTo(42));
            Assert.That(GetReceivedMessageFrameIndex(received2), Is.EqualTo(44));
            Assert.That(received3, Is.Not.Null);
            Assert.That(received3, Is.SameAs(received2));
            Assert.That(GetReceivedMessageFrameIndex(received3), Is.EqualTo(44));
            Assert.That(received3.ExtraData.ToArray(), Is.EqualTo(extraData3));
            Assert.That(received4, Is.Not.Null);
            Assert.That(received4ExtraData, Is.SameAs(received2ExtraData)); //* See below
            Assert.That(GetReceivedMessageFrameIndex(received4), Is.EqualTo(45));
            Assert.That(received4.ExtraData.ToArray(), Is.EqualTo(extraData4));

            //* Why is it received4ExtraData that is the same as received2ExtraData and not received3ExtraData?
            //  This is cause by the current implementation where the "receive buffer" is immediately picked up by the
            //  receiving loop and so received3ExtraData use a byte[] that was allocated before received2.Dispose(), so
            //  the returned byte[] is then used to receive the data of the 4th message.
        }

        [Test]
        public void DealsWithDoubleReceivedMessageDispose()
        {
            using var sender = new UdpAgent(GetConfig(null));
            using var receiver = new UdpAgent(GetConfig(new [] {MessageType.RegisteringWithEmitter}));

            // Send
            var messageToSend = new RegisteringWithEmitter()
            {
                NodeId = 42,
                IPAddressBytes = BitConverter.ToUInt32(sender.AdapterAddress.GetAddressBytes())
            };
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);

            // Receive the message
            var received1 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            Assert.That(received1, Is.Not.Null);

            // Dispose it twice
            received1.Dispose();
            received1.Dispose();
            LogAssert.Expect(LogType.Warning,"Dispose called twice on ReceivedMessage<RegisteringWithEmitter>");

            // Generated a warning, but everything should still work
            messageToSend.NodeId = 28;
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);
            sender.SendMessage(MessageType.RegisteringWithEmitter, messageToSend);
            var received2 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));
            var received3 = receiver.TryConsumeNextReceivedMessage(TimeSpan.FromSeconds(10));

            Assert.That(received2, Is.Not.Null);
            Assert.That(received2, Is.SameAs(received1));
            Assert.That(received3, Is.Not.Null);
            Assert.That(received3, Is.Not.SameAs(received1));
            Assert.That(received3, Is.Not.SameAs(received2));
        }

        static UdpAgentConfig GetConfig(MessageType[] receivedMessagesType)
        {
            return new UdpAgentConfig()
            {
                MulticastIp = IPAddress.Parse("224.0.1.0"),
                Port = k_TestPort,
                ReceivedMessagesType = receivedMessagesType
            };
        }

        static byte[] AllocRandomData(int length)
        {
            var ret = new byte[length];
            for (int currentPosition = 0; currentPosition < length; currentPosition += 16)
            {
                var toCopy = Guid.NewGuid().ToByteArray().AsSpan(new Range(0, Math.Min(length - currentPosition, 16)));
                toCopy.CopyTo(new Span<byte>(ret, currentPosition, length - currentPosition));
            }
            return ret;
        }

        const int k_TestPort = 25691;
    }
}
