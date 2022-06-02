using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.Utilities;

namespace Unity.ClusterDisplay.Tests
{
    public class EmitFrameStateTests
    {
        [Test]
        public void SendFrame0()
        {
            using var testState = new EmitFrameState(m_Node);

            byte[] stateData0 = AllocRandomByteArray(500);
            m_StateDataQueue.Enqueue(stateData0);
            byte[] customData0 = AllocRandomByteArray(500);
            m_CustomDataQueue.Enqueue(customData0);

            long lastReadyTimestamp = long.MaxValue;
            var sendRepeatersReadyTask = Task.Run(() =>
            {
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[0], true);
                Thread.Sleep(10);
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[1], true);
                Thread.Sleep(15);
                lastReadyTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[2], true);
            });

            var nextState = testState.DoFrame();
            long doFrameEndTimestamp = Stopwatch.GetTimestamp();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);
            Assert.That(doFrameEndTimestamp, Is.GreaterThanOrEqualTo(lastReadyTimestamp));

            var receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(3));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 0, stateData0, customData0);

            Assert.That(m_Node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(k_RepeaterNodeId.Length));
            foreach (var repeaterNodeId in k_RepeaterNodeId)
            {
                Assert.That(m_Node.RepeatersStatus[repeaterNodeId].IP, Is.Not.EqualTo(IPAddress.None));
                Assert.That(m_Node.RepeatersStatus.RepeaterPresence[repeaterNodeId], Is.True);
            }
        }

        [Test]
        public void OneRepeaterTimeout()
        {
            ReplaceNodeWithNewOne(TimeSpan.FromMilliseconds(250), false);
            using var testState = new EmitFrameState(m_Node);

            byte[] stateData0 = AllocRandomByteArray(500);
            m_StateDataQueue.Enqueue(stateData0);
            byte[] customData0 = AllocRandomByteArray(500);
            m_CustomDataQueue.Enqueue(customData0);

            var sendRepeatersReadyTask = Task.Run(() =>
            {
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[0], true);
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[2], true);
            });

            var nextState = testState.DoFrame();
            LogAssert.Expect(LogType.Error, $"Repeaters {k_RepeaterNodeId[1]} did not signaled they were ready within 0.25 seconds, they will be dropped from the cluster.");
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);

            var receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 0, stateData0, customData0);

            Assert.That(k_RepeaterNodeId.Length, Is.EqualTo(3));
            Assert.That(m_Node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(2));
            Assert.That(m_Node.RepeatersStatus[k_RepeaterNodeId[0]].IP, Is.EqualTo(IPAddress.Parse("1.2.3." + k_RepeaterNodeId[0])));
            Assert.That(m_Node.RepeatersStatus.RepeaterPresence[k_RepeaterNodeId[0]], Is.True);
            Assert.That(m_Node.RepeatersStatus[k_RepeaterNodeId[1]].IP, Is.Null);
            Assert.That(m_Node.RepeatersStatus.RepeaterPresence[k_RepeaterNodeId[1]], Is.False);
            Assert.That(m_Node.RepeatersStatus[k_RepeaterNodeId[2]].IP, Is.EqualTo(IPAddress.Parse("1.2.3." + k_RepeaterNodeId[2])));
            Assert.That(m_Node.RepeatersStatus.RepeaterPresence[k_RepeaterNodeId[2]], Is.True);
        }

        [Test]
        public void MultipleFramesWithLessNetworkSync()
        {
            using var testState = new EmitFrameState(m_Node);

            // Frame 0

            byte[] stateData0 = AllocRandomByteArray(500);
            m_StateDataQueue.Enqueue(stateData0);
            byte[] customData0 = AllocRandomByteArray(500);
            m_CustomDataQueue.Enqueue(customData0);

            long lastReadyTimestamp = long.MaxValue;
            var sendRepeatersReadyTask = Task.Run(() =>
            {
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[0], false);
                Thread.Sleep(10);
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[1], true);
                Thread.Sleep(15);
                lastReadyTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[2], false);
            });

            var nextState = testState.DoFrame();
            long doFrameEndTimestamp = Stopwatch.GetTimestamp();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);
            Assert.That(doFrameEndTimestamp, Is.GreaterThanOrEqualTo(lastReadyTimestamp));

            var receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(3));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 0, stateData0, customData0);

            m_Node.ConcludeFrame();

            // Frame 1

            byte[] stateData1 = AllocRandomByteArray(600);
            m_StateDataQueue.Enqueue(stateData1);
            byte[] customData1 = AllocRandomByteArray(400);
            m_CustomDataQueue.Enqueue(customData1);

            SendRepeaterWaitingToStartFrame(1, k_RepeaterNodeId[1], false);

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);

            receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(1));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 1, stateData1, customData1);

            m_Node.ConcludeFrame();

            // Frame 2

            byte[] stateData2 = AllocRandomByteArray(300);
            m_StateDataQueue.Enqueue(stateData2);
            byte[] customData2 = AllocRandomByteArray(300);
            m_CustomDataQueue.Enqueue(customData2);

            nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);

            receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(1));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 2, stateData2, customData2);

            // Check RepeatersStatus still ok
            Assert.That(m_Node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(k_RepeaterNodeId.Length));
            foreach (var repeaterNodeId in k_RepeaterNodeId)
            {
                Assert.That(m_Node.RepeatersStatus[repeaterNodeId].IP, Is.Not.EqualTo(IPAddress.None));
                Assert.That(m_Node.RepeatersStatus.RepeaterPresence[repeaterNodeId], Is.True);
            }
        }

        [Test]
        public void DelayedRepeater()
        {
            ReplaceNodeWithNewOne(m_MaxTestTime, true);
            using var testState = new EmitFrameState(m_Node);

            // DoFrame and conclude frame should not try to wait after repeaters and should not send anything either.
            byte[] stateData0 = AllocRandomByteArray(500);
            m_StateDataQueue.Enqueue(stateData0);
            var nextState = testState.DoFrame();
            Assert.That(nextState, Is.Null);
            Assert.That(m_RepeaterAgent.ReceivedMessagesCount, Is.Zero);
            Assert.That(m_StateDataQueue, Is.Empty);
            m_Node.ConcludeFrame();

            // Now start frame 1 (customData for frame 0 will be consumed one frame late)
            byte[] customData0 = AllocRandomByteArray(500);
            m_CustomDataQueue.Enqueue(customData0);
            byte[] stateData1 = AllocRandomByteArray(550);
            m_StateDataQueue.Enqueue(stateData1);

            long lastReadyTimestamp = long.MaxValue;
            var sendRepeatersReadyTask = Task.Run(() =>
            {
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[0], true);
                Thread.Sleep(10);
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[1], true);
                Thread.Sleep(15);
                lastReadyTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[2], true);
            });

            nextState = testState.DoFrame();
            long doFrameEndTimestamp = Stopwatch.GetTimestamp();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);
            Assert.That(doFrameEndTimestamp, Is.GreaterThanOrEqualTo(lastReadyTimestamp));

            var receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(3));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 0, stateData0, customData0);

            Assert.That(m_Node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(k_RepeaterNodeId.Length));
            foreach (var repeaterNodeId in k_RepeaterNodeId)
            {
                Assert.That(m_Node.RepeatersStatus[repeaterNodeId].IP, Is.Not.EqualTo(IPAddress.None));
                Assert.That(m_Node.RepeatersStatus.RepeaterPresence[repeaterNodeId], Is.True);
            }
        }

        [Test]
        public void AnswerRegisteringWithEmitter()
        {
            using var testState = new EmitFrameState(m_Node);

            byte[] stateData0 = AllocRandomByteArray(500);
            m_StateDataQueue.Enqueue(stateData0);
            byte[] customData0 = AllocRandomByteArray(500);
            m_CustomDataQueue.Enqueue(customData0);

            long lastReadyTimestamp = long.MaxValue;
            var sendRepeatersReadyTask = Task.Run(() =>
            {
                // Repeat a valid RegisteringWithEmitter, it should be accepted
                m_RepeaterAgent.SendMessage(MessageType.RegisteringWithEmitter, new RegisteringWithEmitter()
                {
                   NodeId = k_RepeaterNodeId[1],
                   IPAddressBytes = BitConverter.ToUInt32(IPAddress.Parse("1.2.3." + k_RepeaterNodeId[1]).GetAddressBytes())
                });
                Assert.That(m_RepeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using (var receivedMessage = m_RepeaterAgent.ConsumeNextReceivedMessage())
                {
                    Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RepeaterRegistered));
                    var receivedRegisteredMessage = receivedMessage as ReceivedMessage<RepeaterRegistered>;
                    Assert.That(receivedRegisteredMessage, Is.Not.Null);
                    Assert.That(receivedRegisteredMessage.Payload.Accepted, Is.True);
                }

                // But not if the ip is different
                m_RepeaterAgent.SendMessage(MessageType.RegisteringWithEmitter, new RegisteringWithEmitter()
                {
                    NodeId = k_RepeaterNodeId[1],
                    IPAddressBytes = BitConverter.ToUInt32(IPAddress.Parse("4.5.6." + k_RepeaterNodeId[1]).GetAddressBytes())
                });
                Assert.That(m_RepeaterAgent.ReceivedMessagesCount, Is.EqualTo(1));
                using (var receivedMessage = m_RepeaterAgent.ConsumeNextReceivedMessage())
                {
                    Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RepeaterRegistered));
                    var receivedRegisteredMessage = receivedMessage as ReceivedMessage<RepeaterRegistered>;
                    Assert.That(receivedRegisteredMessage, Is.Not.Null);
                    Assert.That(receivedRegisteredMessage.Payload.Accepted, Is.False);
                }

                // Now let's send messages about ready repeaters
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[0], true);
                Thread.Sleep(10);
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[1], true);
                Thread.Sleep(15);
                lastReadyTimestamp = Stopwatch.GetTimestamp();
                SendRepeaterWaitingToStartFrame(0, k_RepeaterNodeId[2], true);
            });

            var nextState = testState.DoFrame();
            long doFrameEndTimestamp = Stopwatch.GetTimestamp();
            Assert.That(nextState, Is.Null);
            Assert.DoesNotThrow(sendRepeatersReadyTask.Wait);
            Assert.That(doFrameEndTimestamp, Is.GreaterThanOrEqualTo(lastReadyTimestamp));

            var receivedByRepeaters = ConsumeAllReceivedRepeaterMessages();
            Assert.That(receivedByRepeaters.Keys.Count, Is.EqualTo(2));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.EmitterWaitingToStartFrame), Is.True);
            Assert.That(receivedByRepeaters[MessageType.EmitterWaitingToStartFrame].Count, Is.EqualTo(3));
            Assert.That(receivedByRepeaters.Keys.Contains(MessageType.FrameData), Is.True);
            Assert.That(receivedByRepeaters[MessageType.FrameData].Count, Is.EqualTo(1));
            TestReceivedData(receivedByRepeaters[MessageType.FrameData].FirstOrDefault(), 0, stateData0, customData0);

            Assert.That(m_Node.RepeatersStatus.RepeaterPresence.SetBitsCount, Is.EqualTo(k_RepeaterNodeId.Length));
            foreach (var repeaterNodeId in k_RepeaterNodeId)
            {
                Assert.That(m_Node.RepeatersStatus[repeaterNodeId].IP, Is.Not.EqualTo(IPAddress.None));
                Assert.That(m_Node.RepeatersStatus.RepeaterPresence[repeaterNodeId], Is.True);
            }
        }

        [SetUp]
        public void Setup()
        {
            var udpAgentNetwork = new TestUDPAgentNetwork();
            m_RepeaterAgent = new TestUDPAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray());

            var nodeConfig = new ClusterNodeConfig()
            {
                NodeId = k_NodeId,
                HandshakeTimeout = m_MaxTestTime,
                CommunicationTimeout = m_MaxTestTime
            };

            var emitterNodeConfig = new EmitterNodeConfig()
            {
                ExpectedRepeaterCount = (byte)k_RepeaterNodeId.Length
            };

            m_Node = new EmitterNode(nodeConfig, emitterNodeConfig,
                new TestUDPAgent(udpAgentNetwork, EmitterNode.ReceiveMessageTypes.ToArray()));
            AddRepeaterNodesToEmitter();

            var stateDelegatesOverride = new (int, FrameDataBuffer.StoreDataDelegate)[] {((int)StateID.Time, StoreNextStateData)};
            EmitterStateWriter.OverrideStateDelegates(stateDelegatesOverride);
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate(k_DataBlockTypeId, StoreNextCustomData);
        }

        [TearDown]
        public void TearDown()
        {
            EmitterStateWriter.UnregisterCustomDataDelegate(k_DataBlockTypeId, StoreNextCustomData);
            EmitterStateWriter.ClearStateDelegatesOverride();
            m_Node?.Dispose();
            m_Node = null;
        }

        int StoreNextStateData(NativeArray<byte> writeableBuffer)
        {
            Assert.That(m_StateDataQueue, Is.Not.Empty);
            byte[] data = m_StateDataQueue.Dequeue();
            NativeArray<byte>.Copy(data, 0, writeableBuffer, 0, data.Length);
            return data.Length;
        }

        int StoreNextCustomData(NativeArray<byte> writeableBuffer)
        {
            Assert.That(m_CustomDataQueue, Is.Not.Empty);
            byte[] data = m_CustomDataQueue.Dequeue();
            NativeArray<byte>.Copy(data, 0, writeableBuffer, 0, data.Length);
            return data.Length;
        }

        static void TestReceivedData(ReceivedMessageBase receivedMessage, ulong frameIndex, byte[] stateData, byte[] customData)
        {
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.FrameData));
            var receivedFrameData = receivedMessage as ReceivedMessage<FrameData>;
            Assert.That(receivedFrameData, Is.Not.Null);
            Assert.That(receivedFrameData.Payload.FrameIndex, Is.EqualTo(frameIndex));

            var frameDataReader = new FrameDataReader(receivedMessage.ExtraData.AsNativeArray());
            var frameDataItemsEnumerator = frameDataReader.GetEnumerator();
            Assert.That(frameDataItemsEnumerator, Is.Not.Null);
            Assert.That(frameDataItemsEnumerator.MoveNext(), Is.True);
            var firstItem = frameDataItemsEnumerator.Current;
            Assert.That(firstItem.id, Is.EqualTo((int)StateID.Time));
            Assert.That(firstItem.data.ToArray(), Is.EqualTo(stateData));
            Assert.That(frameDataItemsEnumerator.MoveNext(), Is.True);
            var secondItem = frameDataItemsEnumerator.Current;
            Assert.That(secondItem.id, Is.EqualTo(k_DataBlockTypeId));
            Assert.That(secondItem.data.ToArray(), Is.EqualTo(customData));
        }

        void SendRepeaterWaitingToStartFrame(ulong frameIndex, byte nodeId, bool willUseNetworkSyncOnNextFrame)
        {
            m_RepeaterAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame, new RepeaterWaitingToStartFrame()
            {
                FrameIndex = frameIndex,
                NodeId = nodeId,
                WillUseNetworkSyncOnNextFrame = willUseNetworkSyncOnNextFrame
            });
        }

        Dictionary<MessageType,List<ReceivedMessageBase>> ConsumeAllReceivedRepeaterMessages()
        {
            Dictionary<MessageType, List<ReceivedMessageBase>> ret = new();
            for (;;)
            {
                var receivedMessage = m_RepeaterAgent.TryConsumeNextReceivedMessage();
                if (receivedMessage != null)
                {
                    ret.TryAdd(receivedMessage.Type, new List<ReceivedMessageBase>());
                    ret[receivedMessage.Type].Add(receivedMessage);
                }
                else
                {
                    break;
                }
            }

            return ret;
        }

        void ReplaceNodeWithNewOne(TimeSpan communicationTimeout, bool delayedRepeater)
        {
            var newNodeConfig = m_Node.Config;
            newNodeConfig.CommunicationTimeout = communicationTimeout;
            newNodeConfig.RepeatersDelayed = delayedRepeater;
            var newEmitterNodeConfig = m_Node.EmitterConfig;
            IUDPAgent repeaterUdpAgent = m_Node.UdpAgent;
            m_Node.Dispose();
            m_Node = new EmitterNode(newNodeConfig, newEmitterNodeConfig, repeaterUdpAgent);
            AddRepeaterNodesToEmitter();
        }

        void AddRepeaterNodesToEmitter()
        {
            foreach (var nodeId in k_RepeaterNodeId)
            {
                m_Node.RepeatersStatus.ProcessRegisteringMessage(new RegisteringWithEmitter()
                {
                    IPAddressBytes =  BitConverter.ToUInt32(IPAddress.Parse($"1.2.3." + nodeId).GetAddressBytes()),
                    NodeId = nodeId
                });
            }
        }

        const byte k_NodeId = 42;
        static byte[] k_RepeaterNodeId = new byte[] {5, 105, 205};
        const int k_DataBlockTypeId = 28;

        TimeSpan m_MaxTestTime = TimeSpan.FromSeconds(10);
        EmitterNode m_Node;
        TestUDPAgent m_RepeaterAgent;
        Queue<byte[]> m_StateDataQueue = new ();
        Queue<byte[]> m_CustomDataQueue = new ();
    }
}
