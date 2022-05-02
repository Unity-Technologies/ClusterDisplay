using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class RepeaterNodeTests
    {
        UDPAgent m_TestAgent;
        NodeState m_State;
        RepeaterNode m_Node;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;

        [SetUp]
        public void SetUp()
        {
            var testAgentConfig = udpConfig;
            testAgentConfig.nodeId = k_EmitterId;
            testAgentConfig.rxPort = udpConfig.txPort;
            testAgentConfig.txPort = udpConfig.rxPort;

            m_TestAgent = new UDPAgent(testAgentConfig);

            RepeaterStateReader.ClearOnLoadDataDelegates();
            var repeaterUdpConfig = udpConfig;
            repeaterUdpConfig.nodeId = k_RepeaterId;
            m_Node = new RepeaterNode(new RepeaterNode.Config
            {
                MainConfig =
                {
                    UdpAgentConfig = repeaterUdpConfig
                }
            });
            Assert.IsTrue(m_Node.EmitterNodeIdMask[k_EmitterId]);
        }

        [Test]
        public void TestRegisterWithEmitter()
        {
            // Create the state under test
            var registerState = new RegisterWithEmitter(m_Node);

            var allNodesMask = m_Node.UdpAgent.AllNodesMask;
            Assert.IsFalse(allNodesMask[k_EmitterId]);

            // Before receiving a WelcomeRepeater, we should be staying in this state
            registerState.EnterState(null);
            Assert.That(registerState.ProcessFrame(true), Is.EqualTo(registerState));

            // The state should be broadcasting HelloEmitter messages
            var (header, rolePublication) = m_TestAgent.ReceiveMessage<RolePublication>();

            Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
            Assert.That(rolePublication.NodeRole, Is.EqualTo(NodeRole.Repeater));

            // ReadyToProceed should be false. This state does not allow advancement of frames
            Assert.IsFalse(registerState.ReadyToProceed);

            // Send an acceptance message
            m_TestAgent.PublishMessage(new MessageHeader
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = BitVector.FromIndex(header.OriginID),
            });

            // Wait for the state to transition
            m_State = RunStateUntilTransition(registerState);
            Assert.That(m_State, Is.TypeOf<RepeaterSynchronization>());

            allNodesMask = m_Node.UdpAgent.AllNodesMask;
            Assert.IsTrue(allNodesMask[k_EmitterId]);
        }

        [UnityTest]
        public IEnumerator TestRepeaterSynchronization()
        {
            var repeaterSynchronization = new RepeaterSynchronization(m_Node);
            repeaterSynchronization.EnterState(null);

            // listener for custom frame data
            const byte customId = 42;
            const int magicNumber = 12345;
            var customDataCallCount = 0;
            RepeaterStateReader.RegisterOnLoadDataDelegate(customId, data =>
            {
                if (data.LoadStruct<int>() == magicNumber)
                {
                    customDataCallCount++;
                }
                return true;
            });

            // Buffer to hold custom data
            using var frameData = new FrameDataBuffer(128);
            var customData = new byte[128];

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Repeater frame ===========
                Assert.AreEqual(m_Node.CurrentFrameID, frameNum);

                // The state should waiting for the emitter data that marks the sync point
                // at the beginning of the frame
                Assert.IsFalse(repeaterSynchronization.ReadyToProceed);
                Assert.IsTrue(RunStateUpdateUntil(repeaterSynchronization,
                    state => state.Stage == RepeaterSynchronization.EStage.WaitingOnEmitterFrameData));

                // Simulate a EmitterLastFrameData message from the emitter
                // The FrameData contains just a custom message.
                frameData.Clear();
                var someValue = magicNumber;
                frameData.Store(customId, ref someValue);
                frameData.Store((byte) StateID.End);
                frameData.CopyTo(customData);
                var (header, lastFrameMsg) = GenerateMessage(k_EmitterId,
                    new byte[] {k_RepeaterId},
                    EMessageType.LastFrameData,
                    new EmitterLastFrameData()
                    {
                        FrameNumber = frameNum
                    },
                    MessageHeader.EFlag.Broadcast,
                    customData);

                m_TestAgent.PublishMessage(header, lastFrameMsg);

                // LastFrameData received. Continue with the frame.
                Assert.IsTrue(RunStateUntilReadyToProceed(repeaterSynchronization));
                Assert.IsTrue(RunStateUntilReadyForNextFrame(repeaterSynchronization));

                // ======= End of Frame ===========
                m_Node.EndFrame();
                yield return null;

                // ======= Start of frame > 0 ===========
                // Do the EnteredNextFrame signal exchange
                m_State = repeaterSynchronization.ProcessFrame(newFrame: true);
                Assert.That(repeaterSynchronization.Stage, Is.EqualTo(RepeaterSynchronization.EStage.EnteredNextFrame));

                // Repeater signals start of new frame and waits for ack
                m_State = repeaterSynchronization.ProcessFrame(false);
                Assert.That(repeaterSynchronization.Stage, Is.EqualTo(RepeaterSynchronization.EStage.WaitForEmitterACK));

                // Emitter expects to receive a signal that node as entered a new frame
                // (ack is taken care of internally by UDPAgent)
                var receiveMessage = m_TestAgent
                    .ReceiveMessageAsync<RepeaterEnteredNextFrame>()
                    .ToCoroutine();

                yield return receiveMessage.WaitForCompletion(TimeoutSeconds);

                Assert.True(receiveMessage.IsSuccessful);
                var (rxHeader, _) = receiveMessage.Result;
                Assert.That(rxHeader.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));
            }

            Assert.That(customDataCallCount, Is.EqualTo(kNumFrames));
        }

        [UnityTest]
        public IEnumerator TestRepeaterSynchronizationHardwareSync()
        {
            var repeaterSynchronization = new RepeaterSynchronization(m_Node)
            {
                HasHardwareSync = true
            };
            repeaterSynchronization.EnterState(null);

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Repeater frame ===========
                Assert.AreEqual(m_Node.CurrentFrameID, frameNum);

                // The state should waiting for the emitter data that marks the sync point
                // at the beginning of the frame
                repeaterSynchronization.ProcessFrame(true);
                Assert.IsFalse(repeaterSynchronization.ReadyToProceed);
                Assert.IsTrue(RunStateUpdateUntil(repeaterSynchronization,
                    state => state.Stage == RepeaterSynchronization.EStage.WaitingOnEmitterFrameData));

                // Simulate a EmitterLastFrameData message from the emitter
                var (header, lastFrameMsg) = GenerateMessage(k_EmitterId,
                    new byte[] {k_RepeaterId},
                    EMessageType.LastFrameData,
                    new EmitterLastFrameData()
                    {
                        FrameNumber = frameNum
                    },
                    MessageHeader.EFlag.Broadcast,
                    Enumerable.Repeat<byte>(0, 32).ToArray()); // Pad with zeros to signal end of data

                m_TestAgent.PublishMessage(header, lastFrameMsg);

                // LastFrameData received. Continue with the frame.
                Assert.IsTrue(RunStateUntilReadyToProceed(repeaterSynchronization));
                Assert.IsTrue(RunStateUntilReadyForNextFrame(repeaterSynchronization));

                // ======= End of Frame ===========
                m_Node.EndFrame();
                yield return null;
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Awkward way to invoke m_State.ExitState
            new NullState().EnterState(m_State);

            m_Node.Exit();
            m_TestAgent.Dispose();
        }
    }
}
