using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using static Unity.ClusterDisplay.Tests.Utilities;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable LoopVariableIsNeverChangedInsideLoop

namespace Unity.ClusterDisplay.Tests
{
    public class EmitterPlaceholderTests
    {
        void Setup(byte repeaterCount)
        {
            List<ClusterTopologyEntry> clusterTopologyEntries = new();
            clusterTopologyEntries.Add(new() {NodeRole = NodeRole.Emitter, NodeId = k_EmitterNodeId});

            TestUdpAgentNetwork udpAgentNetwork = new();
            m_RepeatersAgents = new();
            for (int i = 0; i < repeaterCount; ++i)
            {
                m_RepeatersAgents.Add(new TestUdpAgent(udpAgentNetwork, RepeaterNode.ReceiveMessageTypes.ToArray()));
                byte nodeId = GetRepeaterNodeId(m_RepeatersAgents.Last());
                clusterTopologyEntries.Add(new() {NodeRole = NodeRole.Repeater, NodeId = nodeId, RenderNodeId = nodeId});
            }

            m_ClusterTopology.Entries = clusterTopologyEntries;

            m_Placeholder = new(m_ClusterTopology,
                new TestUdpAgent(udpAgentNetwork, EmitterPlaceholder.ReceiveMessageTypes.ToArray()));
        }

        [TearDown]
        public void TearDown()
        {
            m_Placeholder?.Dispose();
            m_Placeholder = null;
        }

        [Test]
        public void TwoRepeatersWithSameFrame()
        {
            Setup(2);

            var repeatersTask = m_RepeatersAgents.Select(r => Task.Run(() =>
            {
                var repeaterAgent = r;

                // We should first receive a survey request
                using var receivedSurveyRepeatersMessage = repeaterAgent.ConsumeMessagesUntil<SurveyRepeaters>(
                    k_MaxTestTime, _ => true);
                TestIsValidSurveyRepeatersMessage(receivedSurveyRepeatersMessage);

                // that we respond to
                var surveyResponse = GetSurveyRepeaterStatus(repeaterAgent, 28, false);
                repeaterAgent.SendMessage(MessageType.RepeatersSurveyAnswer, surveyResponse);
            })).ToList();

            WaitForRepeatersSynchronized();
            Assert.DoesNotThrow(Task.WhenAll(repeatersTask).Wait);
        }

        [Test]
        [TestCase(500,0)]
        [TestCase(500,1)]
        [TestCase(5000,0)]
        [TestCase(5000,5)]
        public void TwoRepeatersWithOneBehind(int frameSize, int missingDatagramsCount)
        {
            Setup(2);

            byte[] frame28Data = AllocRandomByteArray(frameSize);

            bool exitRepeatersLoop = false;
            var repeater0Task = Task.Run(() =>
            {
                var repeaterAgent = m_RepeatersAgents[0];
                var nodeId = GetRepeaterNodeId(repeaterAgent);

                bool hasFrame28 = false;
                HashSet<int> frame28ReceivedDatagrams = new();
                int frame28ReceivedData = 0;

                var startTime = Stopwatch.StartNew();
                while (startTime.Elapsed < k_MaxTestTime && !exitRepeatersLoop)
                {
                    using var nextMessage = repeaterAgent.TryConsumeNextReceivedMessage(k_MaxWaitOnSingleMessage);
                    if (nextMessage is ReceivedMessage<SurveyRepeaters>)
                    {
                        var surveyResponse = GetSurveyRepeaterStatus(repeaterAgent, (ulong)(hasFrame28 ? 28 : 27),
                            false);
                        repeaterAgent.SendMessage(MessageType.RepeatersSurveyAnswer, surveyResponse);
                    }
                    else if (nextMessage is ReceivedMessage<RetransmitReceivedFrameData> retransmitReceivedFrameData &&
                             retransmitReceivedFrameData.Payload.NodeId == nodeId)
                    {
                        Assert.That(retransmitReceivedFrameData.Payload.FrameIndex, Is.EqualTo(28)); // The only frame we should be asked about

                        RetransmitReceivedData(repeaterAgent, retransmitReceivedFrameData!.Payload.FrameIndex, null);
                    }
                    else if (nextMessage is ReceivedMessage<FrameData> frameData)
                    {
                        Assert.That(frameData.Payload.FrameIndex, Is.EqualTo(28));
                        if (!frame28ReceivedDatagrams.Contains(frameData.Payload.DatagramIndex))
                        {
                            int datagramDataStart = frameData.Payload.DatagramDataOffset;
                            Assert.That(datagramDataStart, Is.LessThan(frame28Data.Length));
                            int datagramDataEnd = frameData.Payload.DatagramDataOffset + frameData.ExtraData.Length;
                            Assert.That(datagramDataEnd, Is.LessThanOrEqualTo(frame28Data.Length));

                            frameData.ExtraData.AsManagedArray(out var extraDataArray, out var extraDataArrayStart,
                                out var extraDataArrayLength);
                            int extraDataArrayEnd = extraDataArrayStart + extraDataArrayLength;
                            Assert.That(extraDataArray[extraDataArrayStart..extraDataArrayEnd],
                                Is.EqualTo(frame28Data[datagramDataStart..datagramDataEnd]));

                            frame28ReceivedData += frameData.ExtraData.Length;
                            frame28ReceivedDatagrams.Add(frameData.Payload.DatagramIndex);
                            if (frame28ReceivedData == frame28Data.Length)
                            {
                                hasFrame28 = true;
                            }
                        }
                    }

                    // Ask for retransmission
                    if (!hasFrame28)
                    {
                        repeaterAgent.SendMessage(MessageType.RetransmitFrameData, new RetransmitFrameData()
                            {FrameIndex = 28, DatagramIndexIndexStart = 0, DatagramIndexIndexEnd = int.MaxValue});
                    }
                }
            });

            int nbrFrame28Retransmit = 0;
            int currentMissingDatagramCount = missingDatagramsCount;
            var repeater1Task = Task.Run(() =>
            {
                var repeaterAgent = m_RepeatersAgents[1];
                var nodeId = GetRepeaterNodeId(repeaterAgent);

                // We should first receive a survey request
                using var receivedSurveyRepeatersMessage = repeaterAgent.ConsumeMessagesUntil<SurveyRepeaters>(
                    k_MaxTestTime, _ => true);
                TestIsValidSurveyRepeatersMessage(receivedSurveyRepeatersMessage);

                // that we respond to
                var surveyResponse = GetSurveyRepeaterStatus(repeaterAgent, 28, false);
                repeaterAgent.SendMessage(MessageType.RepeatersSurveyAnswer, surveyResponse);

                // We should then be requested frame 28
                var startTime = Stopwatch.StartNew();
                while (startTime.Elapsed < k_MaxTestTime && !exitRepeatersLoop)
                {
                    using var nextMessage = repeaterAgent.TryConsumeNextReceivedMessage(k_MaxWaitOnSingleMessage);
                    if (nextMessage is ReceivedMessage<RetransmitReceivedFrameData> receivedRetransmitReceivedMessage &&
                        receivedRetransmitReceivedMessage.Payload.NodeId == nodeId)
                    {
                        TestIsValidRetransmitReceivedFrameData(receivedRetransmitReceivedMessage, nodeId, 28);

                        ++nbrFrame28Retransmit;
                        RetransmitReceivedData(repeaterAgent, receivedRetransmitReceivedMessage!.Payload.FrameIndex,
                            frame28Data, currentMissingDatagramCount);
                        currentMissingDatagramCount = Math.Max(currentMissingDatagramCount, currentMissingDatagramCount - 1);
                    }
                }
            });

            try
            {
                WaitForRepeatersSynchronized();
            }
            finally
            {
                exitRepeatersLoop = true;
            }
            Assert.DoesNotThrow(Task.WhenAll(repeater0Task).Wait);
            Assert.DoesNotThrow(Task.WhenAll(repeater1Task).Wait);
            Assert.That(nbrFrame28Retransmit, Is.LessThanOrEqualTo(missingDatagramsCount * 5 + 1));
        }

        [Test]
        public void DealingWithNetworkSync()
        {
            Setup(4);
            ulong[] repeatersLatestFrame = {10, 11, 12, 13};
            bool[] usingNetworkSync = {true, false, false, true};
            Dictionary<ulong, byte[]> framesData = new(){{10, AllocRandomByteArray(50)}, {11, AllocRandomByteArray(60)},
                {12, AllocRandomByteArray(70)}, {13, AllocRandomByteArray(80)}};

            Assert.That(repeatersLatestFrame.Length, Is.EqualTo(m_RepeatersAgents.Count));
            Assert.That(usingNetworkSync.Length, Is.EqualTo(m_RepeatersAgents.Count));
            bool exitRepeatersLoop = false;
            var repeatersTask = Enumerable.Range(0, m_RepeatersAgents.Count).Select(repeaterIndex => Task.Run(() =>
            {
                var repeaterAgent = m_RepeatersAgents[repeaterIndex];
                var nodeId = GetRepeaterNodeId(repeaterAgent);

                while (!exitRepeatersLoop)
                {
                    using var nextMessage = repeaterAgent.TryConsumeNextReceivedMessage(k_MaxWaitOnSingleMessage);
                    if (nextMessage is ReceivedMessage<SurveyRepeaters>)
                    {
                        var surveyResponse = GetSurveyRepeaterStatus(repeaterAgent, repeatersLatestFrame[repeaterIndex],
                            usingNetworkSync[repeaterIndex]);
                        repeaterAgent.SendMessage(MessageType.RepeatersSurveyAnswer, surveyResponse);
                    }
                    else if (nextMessage is ReceivedMessage<RetransmitReceivedFrameData> retransmitReceivedFrameData &&
                             retransmitReceivedFrameData.Payload.NodeId == nodeId)
                    {
                        var frameIndex = retransmitReceivedFrameData.Payload.FrameIndex;
                        var dataToTransmit =
                            frameIndex <= repeatersLatestFrame[repeaterIndex] ? framesData[frameIndex] : null;
                        RetransmitReceivedData(repeaterAgent, frameIndex, dataToTransmit);
                    }
                    else if (nextMessage is ReceivedMessage<EmitterWaitingToStartFrame> emitterWaitingToStartFrame)
                    {
                        Assert.That(emitterWaitingToStartFrame.Payload.FrameIndex,
                            Is.LessThanOrEqualTo(repeatersLatestFrame[repeaterIndex] + 1));
                        if (emitterWaitingToStartFrame.Payload.FrameIndex == repeatersLatestFrame[repeaterIndex] + 1)
                        {
                            ++repeatersLatestFrame[repeaterIndex];
                        }
                    }

                    repeaterAgent.SendMessage(MessageType.RepeaterWaitingToStartFrame, new RepeaterWaitingToStartFrame() {
                        FrameIndex = repeatersLatestFrame[repeaterIndex] + 1,
                        NodeId = nodeId,
                        WillUseNetworkSyncOnNextFrame = usingNetworkSync[repeaterIndex]
                    });
                }
            })).ToList();

            try
            {
                WaitForRepeatersSynchronized();
            }
            finally
            {
                exitRepeatersLoop = true;
            }

            var repeatersSurveyResult = m_Placeholder.RepeatersSurveyResult;
            for (int repeaterIndex = 0; repeaterIndex < m_RepeatersAgents.Count; ++repeaterIndex)
            {
                var repeaterAgent = m_RepeatersAgents[repeaterIndex];
                var nodeId = GetRepeaterNodeId(repeaterAgent);
                Assert.That(repeatersSurveyResult.FirstOrDefault(r => r.NodeId == nodeId), Is.Not.Null);
                Assert.That(repeatersSurveyResult.First(r => r.NodeId == nodeId).StillUseNetworkSync,
                    Is.EqualTo(usingNetworkSync[repeaterIndex]));
            }
            Assert.DoesNotThrow(Task.WhenAll(repeatersTask).Wait);
        }

        void WaitForRepeatersSynchronized()
        {
            var timer = Stopwatch.StartNew();
            while (!m_Placeholder.RepeatersSynchronized && timer.Elapsed < k_MaxTestTime)
            {
                Thread.Sleep(50);
            }

            Assert.That(m_Placeholder.RepeatersSynchronized);
        }

        static byte GetRepeaterNodeId(TestUdpAgent repeaterAgent)
        {
            return repeaterAgent.AdapterAddress.GetAddressBytes()[3]; // Convention for this set of unit test, NodeId = 4th ip address number
        }

        static void TestIsValidSurveyRepeatersMessage(ReceivedMessageBase receivedMessage)
        {
            Assert.That(receivedMessage, Is.Not.Null);
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.SurveyRepeaters));
            Assert.That(receivedMessage, Is.TypeOf<ReceivedMessage<SurveyRepeaters>>());
        }

        static RepeatersSurveyAnswer GetSurveyRepeaterStatus(TestUdpAgent repeaterUdpAgent, ulong lastReceivedFrameIndex,
            bool stillUseNetworkSync)
        {
            return new RepeatersSurveyAnswer() {
                NodeId = GetRepeaterNodeId(repeaterUdpAgent),
                IPAddressBytes = BitConverter.ToUInt32(repeaterUdpAgent.AdapterAddress.GetAddressBytes()),
                LastReceivedFrameIndex = lastReceivedFrameIndex,
                StillUseNetworkSync = stillUseNetworkSync
            };
        }

        static void TestIsValidRetransmitReceivedFrameData(ReceivedMessageBase receivedMessage, byte nodeId,
            ulong frameIndex)
        {
            Assert.That(receivedMessage, Is.Not.Null);
            Assert.That(receivedMessage.Type, Is.EqualTo(MessageType.RetransmitReceivedFrameData));
            Assert.That(receivedMessage, Is.TypeOf<ReceivedMessage<RetransmitReceivedFrameData>>());
            var receivedSurveyRepeaters = (ReceivedMessage<RetransmitReceivedFrameData>)receivedMessage;
            Assert.That(receivedSurveyRepeaters.Payload.NodeId, Is.EqualTo(nodeId));
            Assert.That(receivedSurveyRepeaters.Payload.FrameIndex, Is.EqualTo(frameIndex));
        }

        static void RetransmitReceivedData(TestUdpAgent repeaterAgent, ulong frameIndex, byte[] data, int datagramCountToSkip = 0)
        {
            if (data == null)
            {
                repeaterAgent.SendMessage(MessageType.RetransmittedReceivedFrameData, new RetransmittedReceivedFrameData() {FrameIndex = frameIndex, DataLength = -1});
                return;
            }

            const int dataPerDatagram = 137;
            int nbrDatagrams = data.Length / dataPerDatagram;
            if (nbrDatagrams * dataPerDatagram < data.Length)
            {
                ++nbrDatagrams;
            }

            HashSet<int> datagramsToSkip = new();
            Random random = new();
            while (datagramsToSkip.Count < datagramCountToSkip)
            {
                datagramsToSkip.Add(random.Next(nbrDatagrams));
            }

            for (int datagramIdx = 0; datagramIdx < nbrDatagrams; ++datagramIdx)
            {
                if (datagramsToSkip.Contains(datagramIdx))
                {
                    continue;
                }
                int toSendStart = datagramIdx * dataPerDatagram;
                int toSendEnd = Math.Min((datagramIdx + 1) * dataPerDatagram, data.Length);
                using NativeArray<byte> dataToSend = new(data[toSendStart..toSendEnd], Allocator.Persistent);
                repeaterAgent.SendMessage(MessageType.RetransmittedReceivedFrameData, new RetransmittedReceivedFrameData() {FrameIndex = frameIndex, DataLength = data.Length, DatagramIndex = datagramIdx, DatagramDataOffset = datagramIdx * dataPerDatagram}, dataToSend.AsReadOnly());
            }
        }

        readonly ClusterTopology m_ClusterTopology = new();
        List<TestUdpAgent> m_RepeatersAgents = new();
        EmitterPlaceholder m_Placeholder;

        const byte k_EmitterNodeId = 42;
        static readonly TimeSpan k_MaxTestTime = TimeSpan.FromSeconds(10);
        static readonly TimeSpan k_MaxWaitOnSingleMessage = TimeSpan.FromMilliseconds(100);
    }
}
