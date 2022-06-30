using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.Collections;

namespace Unity.ClusterDisplay.Tests
{
    public class RegisterWithEmitterStateTests
    {
        [Test]
        public void EverythingWorking()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using var receivedMessage =
                    emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);

                emitterAgent.SendMessage(MessageType.RepeaterRegistered,
                    GetRepeaterRegistered(receivedMessage, true));
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(RepeatFrameState)));
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        [Test]
        public void Rejected()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using var receivedMessage =
                    emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);

                emitterAgent.SendMessage(MessageType.RepeaterRegistered,
                    GetRepeaterRegistered(receivedMessage, false));
            });

            Assert.Throws<InvalidOperationException>(() =>testState.DoFrame());
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        [Test]
        public void EmitterHasToRepeat()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using (var receivedMessage =
                       emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout))
                {
                    Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);
                }

                using (var receivedMessage =
                       emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout))
                {
                    Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);
                }

                using var lastReceivedMessage =
                    emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(IsValidRegisteringWithEmitter(lastReceivedMessage, testNode), Is.True);

                emitterAgent.SendMessage(MessageType.RepeaterRegistered,
                    GetRepeaterRegistered(lastReceivedMessage, true));
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(RepeatFrameState)));
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        [Test]
        public void LostRepeaterRegistered()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using var receivedMessage =
                    emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);

                int waitingReceivedMessagesCountBefore = emitterAgent.ReceivedMessagesCount;

                // Fake lost RepeaterRegistered by simply not sending it and jumping immediately to sending the
                // FrameData.
                var frameData = new FrameData(); // Simple FrameData, everything at 0 but it's enough for the test
                emitterAgent.SendMessage(MessageType.FrameData, frameData);

                // Sending FrameData should wake up the repeater that will send a lot of RegisteringWithEmitter to
                // ask the emitter to send back the answer
                Thread.Sleep(500);

                // Current implementation should send a RegisteringWithEmitter at +/- every 5 ms, so we should in
                // theory get +/- 100.  Validate we received at least a quarter of that (to avoid random failures
                // because of the random nature of timing things).  25 is still much more than the +/- 2 we should
                // have received if RegisterWithEmitterState wouldn't have increased its speed because of the
                // FrameData it received.
                Assert.That(emitterAgent.ReceivedMessagesCount - waitingReceivedMessagesCountBefore,
                    Is.GreaterThanOrEqualTo(25));

                // Send the acceptance message
                emitterAgent.SendMessage(MessageType.RepeaterRegistered,
                    GetRepeaterRegistered(receivedMessage, true));
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(RepeatFrameState)));
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        [Test]
        public void LostRegisteredAndFirstFrame()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using (var receivedMessage =
                       emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout))
                {
                    Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);
                }

                int waitingReceivedMessagesCountBefore = emitterAgent.ReceivedMessagesCount;

                // Fake lost RepeaterRegistered and FrameData for FrameIndex 0 by simply not sending them and
                // jumping immediately to sending the FrameData for FrameIndex 1.
                var frameData = new FrameData() { FrameIndex = 1 };
                emitterAgent.SendMessage(MessageType.FrameData, frameData);
            });

            Assert.Throws<InvalidDataException>(() => testState.DoFrame());
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        [Test]
        public void Timeout()
        {
            using var testNode = CreateNode(TimeSpan.FromMilliseconds(250), out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            Assert.Throws<TimeoutException>(() => testState.DoFrame());
        }

        [Test]
        public void IgnoreOtherNodeIdAndIP()
        {
            using var testNode = CreateNode(m_MaxTestTime, out var emitterAgent);
            var testState = new RegisterWithEmitterState(testNode);

            var emitterTask = Task.Run(() =>
            {
                using var receivedMessage =
                    emitterAgent.TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(IsValidRegisteringWithEmitter(receivedMessage, testNode), Is.True);

                // Response as accepted but with the wrong node id and ip.
                var message = GetRepeaterRegistered(receivedMessage, true);
                ++message.NodeId;
                emitterAgent.SendMessage(MessageType.RepeaterRegistered, message);
                message = GetRepeaterRegistered(receivedMessage, true);
                message.IPAddressBytes = BitConverter.ToUInt32(new byte[] { 6, 7, 8, 9 });
                emitterAgent.SendMessage(MessageType.RepeaterRegistered, message);

                // And at last, response as failed, if DoFrame succeeds, then it means it considered one of the
                // messages sent before that wasn't mean for that state...
                emitterAgent.SendMessage(MessageType.RepeaterRegistered,
                    GetRepeaterRegistered(receivedMessage, false));
            });

            Assert.Throws<InvalidOperationException>(() =>testState.DoFrame());
            Assert.DoesNotThrow(emitterTask.Wait);
        }

        RepeaterNode CreateNode(TimeSpan handshakeTime, out TestUdpAgent emitterAgent)
        {
            var nodeConfig = new ClusterNodeConfig()
            {
                NodeId = m_NodeId,
                HandshakeTimeout = handshakeTime,
                CommunicationTimeout = m_MaxTestTime
            };

            var udpAgentNetwork = new TestUdpAgentNetwork();
            emitterAgent = new TestUdpAgent(udpAgentNetwork, EmitterNode.ReceiveMessageTypes.ToArray());
            return new RepeaterNode(nodeConfig,
                new TestUdpAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray()));
        }

        bool IsValidRegisteringWithEmitter(ReceivedMessageBase receivedMessage, ClusterNode clusterNode)
        {
            if (receivedMessage == null || receivedMessage.Type != MessageType.RegisteringWithEmitter)
            {
                return false;
            }
            var receivedRegisteringWithEmitter = receivedMessage as ReceivedMessage<RegisteringWithEmitter>;
            return receivedRegisteringWithEmitter != null &&
                receivedRegisteringWithEmitter.Payload.NodeId == m_NodeId &&
                receivedRegisteringWithEmitter.Payload.IPAddressBytes == BitConverter.ToUInt32(clusterNode.UdpAgent.AdapterAddress.GetAddressBytes());
        }

        static RepeaterRegistered GetRepeaterRegistered(ReceivedMessageBase receivedMessage, bool accepted)
        {
            var receivedRegisteringMessage = receivedMessage as ReceivedMessage<RegisteringWithEmitter>;
            Assert.That(receivedRegisteringMessage, Is.Not.Null);
            return new RepeaterRegistered()
            {
                NodeId = receivedRegisteringMessage.Payload.NodeId,
                IPAddressBytes = receivedRegisteringMessage.Payload.IPAddressBytes,
                Accepted = accepted
            };
        }

        byte m_NodeId = 42;
        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
    }
}
