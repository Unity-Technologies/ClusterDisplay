using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay.RepeaterStateMachine;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class RepeaterNodeTests
    {
        MockClusterSync m_ClusterSync;
        UDPAgent m_TestAgent;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;

        [SetUp]
        public void SetUp()
        {
            m_ClusterSync = new MockClusterSync(MockClusterSync.NodeType.Repeater);

            var testConfig = MockClusterSync.udpConfig;
            testConfig.nodeId = k_EmitterId;
            testConfig.rxPort = MockClusterSync.udpConfig.txPort;
            testConfig.txPort = MockClusterSync.udpConfig.rxPort;

            m_TestAgent = new UDPAgent(testConfig);
        }

        [UnityTest]
        public IEnumerator TestRegisterWithEmitter()
        {
            // Create the state under test
            var registerState = new RegisterWithEmitter(m_ClusterSync)
            {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };

            var allNodesMask = m_ClusterSync.LocalNode.UdpAgent.AllNodesMask;
            Assert.Zero(allNodesMask & ((ulong) 1 << k_EmitterId));

            // Before receiving a WelcomeRepeater, we should be staying in this state
            registerState.EnterState(null);
            Assert.That(registerState.ProcessFrame(true), Is.EqualTo(registerState));

            // The state should be broadcasting HelloEmitter messages
            var task = m_TestAgent.ReceiveMessage<RolePublication>().ToCoroutine();
            yield return task.WaitForCompletion(MockClusterSync.timeoutSeconds);

            Assert.IsTrue(task.IsSuccessful);

            var (header, rolePublication) = task.Result;
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
            Assert.That(rolePublication.NodeRole == ENodeRole.Repeater);

            // ReadyToProceed should be false. This state does not allow advancement of frames
            Assert.False(registerState.ReadyToProceed);

            // Send an acceptance message
            m_TestAgent.PublishMessage(new MessageHeader
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = (ulong) 1 << header.OriginID,
            });

            // Wait for the state to transition
            var canExitState = false;
            for (var i = 0; i < MockClusterSync.maxRetries; i++)
            {
                if (registerState.ProcessFrame(true) is RepeaterSynchronization)
                {
                    canExitState = true;
                    break;
                }

                // In practice, frames do not advance in this loop,
                // but here we'll be nice and not lock up the UI during
                // the test
                yield return new WaitForSeconds(0.5f);
            }

            Assert.True(canExitState);

            allNodesMask = m_ClusterSync.LocalNode.UdpAgent.AllNodesMask;
            Assert.NotZero(allNodesMask & ((ulong) 1 << k_EmitterId));
        }

        [UnityTest]
        public IEnumerator TestRepeaterSynchronization()
        {
            var repeaterSynchronization = new RepeaterSynchronization(m_ClusterSync);
            repeaterSynchronization.EnterState(null);

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                m_ClusterSync.CurrentFrameID = frameNum;

                // The state should waiting for the emitter data that marks the sync point
                // at th beginning of the frame
                Assert.False(repeaterSynchronization.ReadyToProceed);
                Assert.AreEqual(repeaterSynchronization.Stage, RepeaterSynchronization.EStage.WaitingOnEmitterFrameData);

                var (header, lastFrameMsg) = GenerateMessage(k_EmitterId, 
                    new byte[] {k_RepeaterId}, 
                    EMessageType.LastFrameData, 
                    new EmitterLastFrameData()
                    {
                        FrameNumber = frameNum
                    },
                    MessageHeader.EFlag.Broadcast, 
                    1); // trailing 0 to indicate empty state data

                m_TestAgent.PublishMessage(header, lastFrameMsg);

                bool canProceed = false;
                for (int i = 0; i < MockClusterSync.maxRetries; i++)
                {
                    repeaterSynchronization.ProcessFrame(false);
                    if (repeaterSynchronization.ReadyToProceed)
                    {
                        canProceed = true;
                        break;
                    }
                    Thread.Sleep(100);
                }
                
                Assert.True(canProceed);

                // In practice, this would be the start of a new frame on the emitter side
                yield return null;
                repeaterSynchronization.ProcessFrame(newFrame: true);
                Assert.AreEqual(repeaterSynchronization.Stage, RepeaterSynchronization.EStage.EnteredNextFrame);
                
                // Repeater signals start of new frame and waits for ack
                repeaterSynchronization.ProcessFrame(false);
                Assert.AreEqual(repeaterSynchronization.Stage, RepeaterSynchronization.EStage.WaitForEmitterACK);

                // Expect to receive a signal that node as entered a new frame
                var receiveMessage = m_TestAgent
                    .ReceiveMessage<RepeaterEnteredNextFrame>()
                    .ToCoroutine();

                yield return receiveMessage.WaitForCompletion(MockClusterSync.timeoutSeconds);
                Assert.True(receiveMessage.IsSuccessful);
                var (rxHeader, contents) = receiveMessage.Result;
                Assert.AreEqual(rxHeader.MessageType, EMessageType.EnterNextFrame);

                // Now wait for the state to transition back to WaitingOnEmitterData
                for (int i = 0; i < MockClusterSync.maxRetries; i++)
                {
                    repeaterSynchronization.ProcessFrame(false);
                    if (repeaterSynchronization.Stage == RepeaterSynchronization.EStage.WaitingOnEmitterFrameData)
                    {
                        break;
                    }
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            m_ClusterSync.LocalNode.Exit();
            m_TestAgent.Stop();
        }
    }
}
