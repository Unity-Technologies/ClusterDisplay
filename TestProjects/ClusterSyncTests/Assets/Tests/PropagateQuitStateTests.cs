using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;

namespace Unity.ClusterDisplay.Tests
{
    public class PropagateQuitStateTests
    {
        [Test]
        public void PropagateToSingle()
        {
            using var testNode = CreateNode(k_MaxTestTime, new byte[]{28}, out var repeatersAgent);
            var testState = new PropagateQuitStateWithoutQuit(testNode);

            var repeaterTask = Task.Run(() =>
            {
                // ReSharper disable once AccessToDisposedClosure -> We wait task completes before disposing so it is ok
                using var receivedMessage =
                    repeatersAgent[0].TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(receivedMessage?.Type, Is.EqualTo(MessageType.PropagateQuit));

                repeatersAgent[0].SendMessage(MessageType.QuitReceived, new QuitReceived() {NodeId = 28});
            });

            var (nextState, doFrameResult) = testState.DoFrame();
            Assert.That(testState.QuitReceived, Is.True);
            Assert.That(nextState, Is.Null);
            Assert.That(doFrameResult, Is.EqualTo(DoFrameResult.FrameDone));
            Assert.DoesNotThrow(repeaterTask.Wait);
        }

        [Test]
        public void PropagateToTwo()
        {
            using var testNode = CreateNode(k_MaxTestTime, new byte[]{28, 56}, out var repeatersAgent);
            var testState = new PropagateQuitStateWithoutQuit(testNode);

            var repeater1Task = Task.Run(() =>
            {
                // ReSharper disable once AccessToDisposedClosure -> We wait task completes before disposing so it is ok
                using var receivedMessage =
                    repeatersAgent[0].TryConsumeNextReceivedMessage(testNode.Config.CommunicationTimeout);
                Assert.That(receivedMessage?.Type, Is.EqualTo(MessageType.PropagateQuit));

                repeatersAgent[0].SendMessage(MessageType.QuitReceived, new QuitReceived() {NodeId = 28});
            });

            var repeater2Task = Task.Run(() =>
            {
                // ReSharper disable once AccessToDisposedClosure -> We wait task completes before disposing so it is ok
                var localTestNode = testNode;

                using var receivedMessage1 =
                    repeatersAgent[1].TryConsumeNextReceivedMessage(localTestNode.Config.CommunicationTimeout);
                Assert.That(receivedMessage1?.Type, Is.EqualTo(MessageType.PropagateQuit));

                // Wait to receive it a second time
                using var receivedMessage2 =
                    repeatersAgent[1].TryConsumeNextReceivedMessage(localTestNode.Config.CommunicationTimeout);
                Assert.That(receivedMessage2?.Type, Is.EqualTo(MessageType.PropagateQuit));

                // And a third
                using var receivedMessage3 =
                    repeatersAgent[1].TryConsumeNextReceivedMessage(localTestNode.Config.CommunicationTimeout);
                Assert.That(receivedMessage3?.Type, Is.EqualTo(MessageType.PropagateQuit));

                // Ok, they waited long enough, lets send the response
                repeatersAgent[1].SendMessage(MessageType.QuitReceived, new QuitReceived() {NodeId = 56});
            });

            var doFrameTimer = Stopwatch.StartNew();
            var (nextState, doFrameResult) = testState.DoFrame();
            doFrameTimer.Stop();
            // DoFrame has to take at least 200 ms as PropagateQuitState repeat every 100 ms and wait to receive the
            // third one before at last sending a reply.
            Assert.That(doFrameTimer.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(200)));
            Assert.That(testState.QuitReceived, Is.True);
            Assert.That(nextState, Is.Null);
            Assert.That(doFrameResult, Is.EqualTo(DoFrameResult.FrameDone));
            Assert.DoesNotThrow(repeater1Task.Wait);
            Assert.DoesNotThrow(repeater2Task.Wait);
        }

        static EmitterNode CreateNode(TimeSpan handshakeTime, IEnumerable<byte> repeatersNodeId,
            out TestUdpAgent[] repeatersAgent)
        {
            var nodeConfig = new ClusterNodeConfig()
            {
                NodeId = k_EmitterNodeId,
                HandshakeTimeout = handshakeTime,
                CommunicationTimeout = k_MaxTestTime
            };

            var emitterNodeConfig = new EmitterNodeConfig()
            {
                ExpectedRepeaterCount = (byte)repeatersNodeId.Count()
            };

            var udpAgentNetwork = new TestUdpAgentNetwork();
            var ret = new EmitterNode(nodeConfig, emitterNodeConfig,
                new TestUdpAgent(udpAgentNetwork, EmitterNode.ReceiveMessageTypes.ToArray()));

            var repeatersAgentList = new List<TestUdpAgent>();
            foreach (var repeaterNodeId in repeatersNodeId)
            {
                ret.RepeatersStatus[repeaterNodeId] = new() {IP = IPAddress.Parse($"1.2.3.{repeaterNodeId}")};
                repeatersAgentList.Add(new TestUdpAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray()));
            }
            repeatersAgent = repeatersAgentList.ToArray();

            return ret;
        }

        class PropagateQuitStateWithoutQuit: PropagateQuitState
        {
            public PropagateQuitStateWithoutQuit(EmitterNode node)
                : base(node)
            {
            }

            public bool QuitReceived { get; private set; }
            protected override void Quit()
            {
                QuitReceived = true;
            }
        }

        const byte k_EmitterNodeId = 42;
        static readonly TimeSpan k_MaxTestTime = TimeSpan.FromSeconds(10);
    }
}
