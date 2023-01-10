using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;
using Utils;

namespace Unity.ClusterDisplay.Tests
{
    public class WelcomeRepeatersStateTests
    {
        [Test]
        public void WelcomeSingle()
        {
            using var testNode = CreateNode(m_MaxTestTime, 1, out var repeatersAgent);
            var testState = new WelcomeRepeatersState(testNode);

            var repeaterTask = Task.Run(() =>
            {
                var registeringMessage = GetRegisteringWithEmitter(repeatersAgent[0]);
                repeatersAgent[0].SendMessage(MessageType.RegisteringWithEmitter, registeringMessage);

                using var receivedMessage = repeatersAgent[0].TryConsumeNextReceivedMessage(m_MaxTestTime);
                Assert.That(IsValidRepeaterRegistered(receivedMessage, registeringMessage, true), Is.True);
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(EmitFrameState)));
            TestNodeState(testNode, repeatersAgent);
            Assert.DoesNotThrow(repeaterTask.Wait);
        }

        [Test]
        public void WelcomeMultiple()
        {
            using var testNode = CreateNode(m_MaxTestTime, 10, out var repeatersAgent);
            var testState = new WelcomeRepeatersState(testNode);

            var repeatersTask = repeatersAgent.Select(agent => Task.Run(() =>
            {
                var registeringMessage = GetRegisteringWithEmitter(agent);
                agent.SendMessage(MessageType.RegisteringWithEmitter, registeringMessage);

                long deadlineTimestamp = StopwatchUtils.TimestampIn(m_MaxTestTime);
                bool found;
                do
                {
                    using var receivedMessage = agent.TryConsumeNextReceivedMessage(m_MaxTestTime);
                    found = IsRepeaterRegisteredFor(receivedMessage, registeringMessage);
                    if (found)
                    {
                        Assert.That(IsValidRepeaterRegistered(receivedMessage, registeringMessage, true), Is.True);
                    }
                } while (!found && Stopwatch.GetTimestamp() < deadlineTimestamp);
                Assert.That(found, Is.True);
            })).ToArray(); // ToArray to force LINQ's Select to be executed and tasks created

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(EmitFrameState)));
            TestNodeState(testNode, repeatersAgent);
            Assert.DoesNotThrow(() => Task.WaitAll(repeatersTask));
        }

        [Test]
        public void MissingRepeater()
        {
            using var testNode = CreateNode(TimeSpan.FromMilliseconds(500), 10, out var repeatersAgent);
            var testState = new WelcomeRepeatersState(testNode);

            var presentRepeatersAgent = new ConcurrentBag<TestUdpAgent>();
            var repeatersTask = repeatersAgent.Select(agent => Task.Run(() =>
            {
                var registeringMessage = GetRegisteringWithEmitter(agent);
                if (registeringMessage.NodeId == 6)
                {   // Do nothing for node 6, so like if is not present
                    return;
                }
                presentRepeatersAgent.Add(agent);
                agent.SendMessage(MessageType.RegisteringWithEmitter, registeringMessage);

                long deadlineTimestamp = StopwatchUtils.TimestampIn(m_MaxTestTime);
                bool found;
                do
                {
                    using var receivedMessage = agent.TryConsumeNextReceivedMessage(m_MaxTestTime);
                    found = IsRepeaterRegisteredFor(receivedMessage, registeringMessage);
                    if (found)
                    {
                        Assert.That(IsValidRepeaterRegistered(receivedMessage, registeringMessage, true), Is.True);
                    }
                } while (!found && Stopwatch.GetTimestamp() < deadlineTimestamp);
                Assert.That(found, Is.True);
            })).ToArray(); // ToArray to force LINQ's Select to be executed and tasks created

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(EmitFrameState)));
            TestNodeState(testNode, presentRepeatersAgent);
            Assert.DoesNotThrow(() => Task.WaitAll(repeatersTask));
        }

        [Test]
        public void MultipleRegistering()
        {
            using var testNode = CreateNode(m_MaxTestTime, 10, out var repeatersAgent);
            var testState = new WelcomeRepeatersState(testNode);

            var repeatersTask = repeatersAgent.Select(agent => Task.Run(() =>
            {
                var randomGenerator = new Random(Guid.NewGuid().GetHashCode());

                var registeringMessage = GetRegisteringWithEmitter(agent);
                int repeatCount = randomGenerator.Next(1, 5);
                for (int i = 0; i < repeatCount; ++i)
                {
                    agent.SendMessage(MessageType.RegisteringWithEmitter, registeringMessage);
                }

                long deadlineTimestamp = StopwatchUtils.TimestampIn(m_MaxTestTime);
                bool found;
                do
                {
                    using var receivedMessage = agent.TryConsumeNextReceivedMessage(m_MaxTestTime);
                    found = IsRepeaterRegisteredFor(receivedMessage, registeringMessage);
                    if (found)
                    {
                        Assert.That(IsValidRepeaterRegistered(receivedMessage, registeringMessage, true), Is.True);
                    }
                } while (!found && Stopwatch.GetTimestamp() < deadlineTimestamp);
                Assert.That(found, Is.True);
            })).ToArray(); // ToArray to force LINQ's Select to be executed and tasks created

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.TypeOf(typeof(EmitFrameState)));
            TestNodeState(testNode, repeatersAgent);
            Assert.DoesNotThrow(() => Task.WaitAll(repeatersTask));
        }

        EmitterNode CreateNode(TimeSpan handshakeTime, byte repeaterCount, out TestUdpAgent[] repeatersAgent)
        {
            var nodeConfig = new ClusterNodeConfig()
            {
                NodeId = m_EmitterNodeId,
                HandshakeTimeout = handshakeTime,
                CommunicationTimeout = m_MaxTestTime
            };

            var emitterNodeConfig = new EmitterNodeConfig()
            {
                ExpectedRepeaterCount = repeaterCount
            };

            var udpAgentNetwork = new TestUdpAgentNetwork();
            var repeatersAgentList = new List<TestUdpAgent>();
            for (int i = 0; i < repeaterCount; ++i)
            {
                repeatersAgentList.Add(new TestUdpAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray()));
            }
            repeatersAgent = repeatersAgentList.ToArray();

            return new EmitterNode(nodeConfig, emitterNodeConfig,
                new TestUdpAgent(udpAgentNetwork, EmitterNode.ReceiveMessageTypes.ToArray()));
        }

        static RegisteringWithEmitter GetRegisteringWithEmitter(TestUdpAgent repeaterUdpAgent)
        {
            return new RegisteringWithEmitter()
            {
                NodeId = repeaterUdpAgent.AdapterAddress.GetAddressBytes()[3], // Convention for this set of unit test, NodeId = 4th ip address number
                IPAddressBytes = BitConverter.ToUInt32(repeaterUdpAgent.AdapterAddress.GetAddressBytes())
            };
        }

        static bool IsRepeaterRegisteredFor(ReceivedMessageBase receivedMessage, RegisteringWithEmitter registeringMessage)
        {
            if (receivedMessage == null || receivedMessage.Type != MessageType.RepeaterRegistered)
            {
                return false;
            }

            return receivedMessage is ReceivedMessage<RepeaterRegistered> receivedRepeaterRegistered &&
                receivedRepeaterRegistered.Payload.NodeId == registeringMessage.NodeId &&
                receivedRepeaterRegistered.Payload.IPAddressBytes == registeringMessage.IPAddressBytes;
        }

        static bool IsValidRepeaterRegistered(ReceivedMessageBase receivedMessage, RegisteringWithEmitter registeringMessage,
            bool accepted)
        {
            return IsRepeaterRegisteredFor(receivedMessage, registeringMessage) &&
                ((ReceivedMessage<RepeaterRegistered>)receivedMessage).Payload.Accepted == accepted;
        }

        static void TestNodeState(EmitterNode emitterNode, IEnumerable<TestUdpAgent> repeaterAgents)
        {
            int repeatersCount = repeaterAgents.Count();
            var repeatersStatus = emitterNode.RepeatersStatus;
            Assert.That(repeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(repeatersCount));
            foreach (var repeaterAgent in repeaterAgents)
            {
                var nodeId = repeaterAgent.AdapterAddress.GetAddressBytes()[3]; // Convention for this set of unit test, NodeId = 4th ip address number
                var repeaterStatus = repeatersStatus[nodeId];
                Assert.That(repeaterStatus.IP, Is.EqualTo(repeaterAgent.AdapterAddress));
                Assert.That(repeatersStatus.RepeaterPresence[nodeId], Is.True);
            }
        }

        byte m_EmitterNodeId = 42;
        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
    }
}
