using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.Collections;
using Utils;
using static Unity.ClusterDisplay.Tests.Utilities;

namespace Unity.ClusterDisplay.Tests
{
    public class RepeatFrameStateTests
    {
        [Test]
        public void ReceiveFrame0()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));
        }

        [Test]
        public void SkipFrame0()
        {
            using var testState = new RepeatFrameState(m_Node);

            SendFrameData(1, 500);

            Assert.Throws<InvalidDataException>(() => testState.DoFrame());
            Assert.That(m_ReceivedData.Count, Is.EqualTo(0));
        }

        [Test]
        public void TwoFrames()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            byte[] frame1Data = null;
            var synchronizeAndSendFrame1Task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 1);

                SendEmitterWaitingToStartFrame(new NodeIdBitVector(), 1);

                // Wait a little bit so that a buggy RepeatFrameState could start repeating RepeaterWaitingToStartFrame
                Thread.Sleep(50);
                ConsumeAllReceivedEmitterMessages();
                Thread.Sleep(50);
                // However, while we were waiting FrameDataAssembler can have started to asked for some data
                // retransmission (since it was informed that everybody was ready).
                var receivedByEmitter = ConsumeAllReceivedEmitterMessages();
                if (receivedByEmitter.Count > 0)
                {
                    Assert.That(receivedByEmitter.ContainsKey(MessageType.RetransmitFrameData), Is.True);
                    Assert.That(receivedByEmitter[MessageType.RetransmitFrameData], Is.GreaterThan(0).And.LessThan(5));
                    Assert.That(receivedByEmitter.Keys.Count, Is.EqualTo(1));
                }

                // Everything looks ok, send data of frame 1
                frame1Data = SendFrameData(1, 500);
            });

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame1Data));
        }

        [Test]
        public void TwoFramesWithDelayedEmitterWaitingToStartFrame()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            byte[] frame1Data = null;
            var synchronizeAndSendFrame1Task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 1);

                // Wait a little bit and we should in theory receive many more repeated RepeaterWaitingToStartFrame as
                // we are not giving any answer, in theory we should receive 1 per ms.  Let's say we will be happy if
                // we receive a quarter of that -> 12.
                Thread.Sleep(50);
                var receivedByEmitter = ConsumeAllReceivedEmitterMessages();
                Assert.That(receivedByEmitter.ContainsKey(MessageType.RepeaterWaitingToStartFrame), Is.True);
                Assert.That(receivedByEmitter[MessageType.RepeaterWaitingToStartFrame], Is.GreaterThanOrEqualTo(12));
                Assert.That(receivedByEmitter.Keys.Count, Is.EqualTo(1));

                // Send a message acknowledging other nodes but not the repeater we are testing
                var stillWaitingOnBitVector = new NodeIdBitVector();
                stillWaitingOnBitVector[k_NodeId] = true;
                // Some other node that is never ready, but it shouldn't matter for the repeater we are testing...
                stillWaitingOnBitVector[k_NodeId + 1] = true;
                SendEmitterWaitingToStartFrame(stillWaitingOnBitVector, 1);
                ConsumeAllReceivedEmitterMessages();

                // Still wait a little bit, same thing, repeater should still be repeating it is waiting...
                Thread.Sleep(50);
                receivedByEmitter = ConsumeAllReceivedEmitterMessages();
                Assert.That(receivedByEmitter.ContainsKey(MessageType.RepeaterWaitingToStartFrame), Is.True);
                Assert.That(receivedByEmitter[MessageType.RepeaterWaitingToStartFrame], Is.GreaterThanOrEqualTo(12));
                Assert.That(receivedByEmitter.Keys.Count, Is.EqualTo(1));

                // Now send a confirmation we know it is ready
                stillWaitingOnBitVector[k_NodeId] = false;
                SendEmitterWaitingToStartFrame(stillWaitingOnBitVector, 1);

                // Wait a little bit and confirm it stopped repeating
                Thread.Sleep(50);
                ConsumeAllReceivedEmitterMessages();
                Thread.Sleep(50);
                // However, while we were waiting FrameDataAssembler can have started to asked for some data
                // retransmission (since it was informed that everybody was ready).
                receivedByEmitter = ConsumeAllReceivedEmitterMessages();
                if (receivedByEmitter.Count > 0)
                {
                    Assert.That(receivedByEmitter.ContainsKey(MessageType.RetransmitFrameData), Is.True);
                    Assert.That(receivedByEmitter[MessageType.RetransmitFrameData], Is.GreaterThan(0).And.LessThan(5));
                    Assert.That(receivedByEmitter.Keys.Count, Is.EqualTo(1));
                }

                // Everything looks ok, send data of frame 1
                frame1Data = SendFrameData(1, 500);
            });

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame1Data));
        }

        [Test]
        public void TwoFramesEmitterWaitingWrongFrameIndex()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            var synchronizeAndSendFrame1Task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 1);

                SendEmitterWaitingToStartFrame(new NodeIdBitVector(), 2);

                // Everything looks ok, send data of frame 1
                SendFrameData(1, 500);
            });

            Assert.Throws<InvalidDataException>(() => testState.DoFrame());
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.Zero);
        }

        [Test]
        public void TwoFramesSkipEmitterWaiting()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            byte[] frame1Data = null;
            var synchronizeAndSendFrame1Task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 1);

                // Everything looks ok, send data of frame 1
                frame1Data = SendFrameData(1, 500);
            });

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame1Data));
        }

        [Test]
        public void NetworkSyncTimeout()
        {
            ReplaceNodeWithNewOne(TimeSpan.FromMilliseconds(250));
            using var testState = new RepeatFrameState(m_Node);

            Assert.Throws<TimeoutException>(() => testState.DoFrame());
            Assert.That(m_ReceivedData.Count, Is.Zero);
        }

        [Test]
        public void FrameDataTimeout()
        {
            ReplaceNodeWithNewOne(TimeSpan.FromMilliseconds(250));
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var synchronizeAndSendFrame1Task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 0);

                SendEmitterWaitingToStartFrame(new NodeIdBitVector(), 0);

                // Wait just a little bit and clear any retransmission request that would be waiting to be consumed
                Thread.Sleep(50);
                ConsumeAllReceivedEmitterMessages();
            });

            var elapsed = new Stopwatch();
            elapsed.Start();
            Assert.Throws<TimeoutException>(() => testState.DoFrame());
            elapsed.Stop();
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.Zero);

            // RepeatFrameState should have received the EmitterWaitingToStartFrame, so it should have stopped
            // transmitting RepeaterWaitingToStartFrame, so emitter queue should be empty.  However we will have +/-
            // 1 RetransmitFrameData request per 30 ms, so +/- 42 (sent by the FrameDataAssembler because WillNeedFrame
            // was called as soon as EmitterWaitingToStartFrame was processed).
            var receivedByEmitter = ConsumeAllReceivedEmitterMessages();
            Assert.That(receivedByEmitter.ContainsKey(MessageType.RetransmitFrameData), Is.True);
            Assert.That(receivedByEmitter[MessageType.RetransmitFrameData], Is.GreaterThanOrEqualTo(20).And.LessThanOrEqualTo(60));
            Assert.That(receivedByEmitter.Keys.Count, Is.EqualTo(1));
        }

        [Test]
        public void ThreeFrameNetworkSync()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            // Start second frame
            var (synchronizeAndSendFrame1Task, frame1Data) = CreateSyncAndSendTask(1, 400, testDeadline);

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame1Data));
            m_ReceivedData.Clear();

            // It is possible that RepeatFrameState had the time to repeat some RepeaterWaitingToStartFrame, so flush
            // to make test code dealing with third frame easier...
            ConsumeAllReceivedEmitterMessages();

            m_Node.ConcludeFrame();

            // Start third frame
            var (synchronizeAndSendFrame2Task, frame2Data) = CreateSyncAndSendTask(2, 600, testDeadline);

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame2Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame2Data));
        }

        [Test]
        public void ThreeFrameHardwareSyncSync()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var (synchronizeAndSendFrame0Task, frame0Data) = CreateSyncAndSendTask(0, 500, testDeadline);

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame0Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame0Data));

            ConsumeAllReceivedEmitterMessages();
            m_ReceivedData.Clear();

            m_Node.ConcludeFrame();

            // Turn on hardware sync before starting frame #2 so that emitter knows not to wait for repeater on frame #3.
            m_Node.UsingNetworkSync = false;

            // Start second frame
            var (synchronizeAndSendFrame1Task, frame1Data) = CreateSyncAndSendTask(1, 500, testDeadline);

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame1Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame1Data));
            m_ReceivedData.Clear();

            // It is possible that RepeatFrameState had the time to repeat some RepeaterWaitingToStartFrame, so flush
            // to make test code dealing with third frame easier...
            ConsumeAllReceivedEmitterMessages();

            m_Node.ConcludeFrame();

            // Start third frame
            byte[] frame2Data = null;
            var synchronizeAndSendFrame2Task = Task.Run(() =>
            {
                frame2Data = SendFrameData(2, 500);
            });

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(synchronizeAndSendFrame2Task.Wait);
            Assert.That(m_ReceivedData.Count, Is.EqualTo(1));
            Assert.That(m_ReceivedData[0], Is.EqualTo(frame2Data));

            // If network synchronization was not done then the emitter shouldn't have received anything from the
            // repeater.
            Assert.That(m_EmitterAgent.ReceivedMessagesCount, Is.Zero);
        }

        [Test]
        public void QuitWhileWaitingFrameSync()
        {
            using var testState = new RepeatFrameState(m_Node);

            var propagateQuitTask = Task.Run(() => {
                m_EmitterAgent.SendMessage(MessageType.PropagateQuit, new PropagateQuit());
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.That(m_Node.QuitReceived, Is.True);
            Assert.DoesNotThrow(propagateQuitTask.Wait);
        }

        [Test]
        public void QuitWhileWaitingFrameData()
        {
            using var testState = new RepeatFrameState(m_Node);
            long testDeadline = StopwatchUtils.TimestampIn(m_MaxTestTime);

            var propagateQuitTask = Task.Run(() => {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, 0);

                SendEmitterWaitingToStartFrame(new NodeIdBitVector(), 0);

                m_EmitterAgent.SendMessage(MessageType.PropagateQuit, new PropagateQuit());
            });

            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.That(m_Node.QuitReceived, Is.True);
            Assert.DoesNotThrow(propagateQuitTask.Wait);
        }

        [SetUp]
        public void Setup()
        {
            var udpAgentNetwork = new TestUdpAgentNetwork();
            m_EmitterAgent = new TestUdpAgent(udpAgentNetwork, EmitterNode.ReceiveMessageTypes.ToArray());

            var nodeConfig = new ClusterNodeConfig()
            {
                NodeId = k_NodeId,
                HandshakeTimeout = m_MaxTestTime,
                CommunicationTimeout = m_MaxTestTime
            };

            m_Node = new RepeaterNodeWithoutQuit(nodeConfig,
                new TestUdpAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray()));

            RepeaterStateReader.RegisterOnLoadDataDelegate(k_DataBlockTypeId, HandleReceivedData);
        }

        [TearDown]
        public void TearDown()
        {
            RepeaterStateReader.UnregisterOnLoadDataDelegate(k_DataBlockTypeId, HandleReceivedData);
            m_Node?.Dispose();
            m_Node = null;
            m_ReceivedData.Clear();
        }

        bool HandleReceivedData(NativeArray<byte> data)
        {
            m_ReceivedData.Add(data.ToArray());
            return true;
        }

        void ReplaceNodeWithNewOne(TimeSpan communicationTimeout)
        {
            var newNodeConfig = m_Node.Config;
            newNodeConfig.CommunicationTimeout = communicationTimeout;
            IUdpAgent repeaterUdpAgent = m_Node.UdpAgent;
            m_Node.Dispose();
            m_Node = new RepeaterNodeWithoutQuit(newNodeConfig, repeaterUdpAgent);
        }

        byte[] SendFrameData(ulong frameIndex, int dataLength)
        {
            var ret = AllocRandomByteArray(dataLength);
            SendFrameData(frameIndex, ret);
            return ret;
        }

        void SendFrameData(ulong frameIndex, byte[] data)
        {
            using var frameBuffer = new FrameDataBuffer();
            frameBuffer.Store(k_DataBlockTypeId, nativeArray =>
            {
                NativeArray<byte>.Copy(data, 0, nativeArray, 0, data.Length);
                return data.Length;
            });

            var frameData = new FrameData()
            {
                FrameIndex = frameIndex,
                DataLength = frameBuffer.Length,
                DatagramIndex = 0,
                DatagramDataOffset = 0
            };

            m_EmitterAgent.SendMessage(MessageType.FrameData, frameData, frameBuffer.Data());
        }

        void SendEmitterWaitingToStartFrame(NodeIdBitVectorReadOnly stillWaitingNods, ulong frameIndex)
        {
            var message = new EmitterWaitingToStartFrame() {FrameIndex = frameIndex};
            stillWaitingNods.CopyTo(out message.WaitingOn0, out message.WaitingOn1, out message.WaitingOn2,
                out message.WaitingOn3);
            m_EmitterAgent.SendMessage(MessageType.EmitterWaitingToStartFrame, message);
        }

        Dictionary<MessageType,int> ConsumeAllReceivedEmitterMessages()
        {
            Dictionary<MessageType, int> ret = new();
            for (;;)
            {
                var receivedMessage = m_EmitterAgent.TryConsumeNextReceivedMessage();
                if (receivedMessage != null)
                {
                    if (!ret.TryAdd(receivedMessage.Type, 1))
                    {
                        ++ret[receivedMessage.Type];
                    }
                    receivedMessage.Dispose();
                }
                else
                {
                    break;
                }
            }

            return ret;
        }

        void TestRepeaterWaitingToStartFrame(ReceivedMessageBase receivedMessage, ulong frameIndex)
        {
            Assert.That(receivedMessage, Is.Not.Null);
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RepeaterWaitingToStartFrame));
            var receivedRepeaterWaiting = receivedMessage as ReceivedMessage<RepeaterWaitingToStartFrame>;
            Assert.That(receivedRepeaterWaiting, Is.Not.Null);
            Assert.That(receivedRepeaterWaiting.Payload.FrameIndex, Is.EqualTo(frameIndex));
            Assert.That(receivedRepeaterWaiting.Payload.NodeId, Is.EqualTo(k_NodeId));
            Assert.That(receivedRepeaterWaiting.Payload.WillUseNetworkSyncOnNextFrame, Is.EqualTo(m_Node.UsingNetworkSync));
        }

        (Task, byte[]) CreateSyncAndSendTask(ulong frameIndex, int dataLength, long testDeadline)
        {
            byte[] frameData = AllocRandomByteArray(dataLength);
            var task = Task.Run(() =>
            {
                using var receivedMessage =
                    m_EmitterAgent.TryConsumeNextReceivedMessage(StopwatchUtils.TimeUntil(testDeadline));
                TestRepeaterWaitingToStartFrame(receivedMessage, frameIndex);

                SendEmitterWaitingToStartFrame(new NodeIdBitVector(), frameIndex);

                SendFrameData(frameIndex, frameData);
            });
            return (task, frameData);
        }

        class RepeaterNodeWithoutQuit: RepeaterNode
        {
            public RepeaterNodeWithoutQuit(ClusterNodeConfig config, IUdpAgent udpAgent)
                : base(config, udpAgent)
            {
            }

            public bool QuitReceived { get; private set; }
            public override void Quit()
            {
                QuitReceived = true;
            }
        }

        const byte k_NodeId = 42;
        const int k_DataBlockTypeId = 28;

        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
        RepeaterNodeWithoutQuit m_Node;
        TestUdpAgent m_EmitterAgent;
        List<byte[]> m_ReceivedData = new ();
    }
}
