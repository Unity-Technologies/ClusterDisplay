using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class EmitterNodeTests
    {
        UDPAgent[] m_TestAgents;
        EmitterNode m_Node;
        const byte k_EmitterId = 0;
        static readonly byte[] k_RepeaterIds = {1, 2};
        NodeState m_State;

        [SetUp]
        public void SetUp()
        {
            m_TestAgents = k_RepeaterIds.Select(id =>
            {
                var testConfig = udpConfig;
                testConfig.nodeId = id;
                testConfig.rxPort = udpConfig.txPort;
                testConfig.txPort = udpConfig.rxPort;
                return new UDPAgent(testConfig);
            }).ToArray();

            var emitterUdpConfig = udpConfig;
            emitterUdpConfig.nodeId = k_EmitterId;
            var emitterConfig = new EmitterNode.Config
            {
                headlessEmitter = false,
                repeatersDelayed = false,
                repeaterCount = k_RepeaterIds.Length,
                MainConfig =
                {
                    UdpAgentConfig = emitterUdpConfig
                }
            };

            m_Node = new EmitterNode(emitterConfig);

            // Watch out for static global states!
            // Potential refactoring target.
            EmitterStateWriter.ClearCustomDataDelegates();
        }

        [Test]
        public void TestWaitForClients()
        {
            Assert.That(m_Node.RemoteNodes, Is.Empty);

            var registerState = new WaitingForAllClients(m_Node);
            registerState.EnterState(null);

            m_State = registerState;

            Assert.AreSame(registerState, registerState.ProcessFrame(false));
            Assert.IsFalse(registerState.ReadyToProceed);

            var helloMsgs = k_RepeaterIds.Select(id => GenerateMessage(id,
                new byte[] {k_EmitterId},
                EMessageType.HelloEmitter,
                new RolePublication {NodeRole = NodeRole.Repeater}));

            foreach (var (msgTuple, udpAgent) in helloMsgs.Zip(m_TestAgents, Tuple.Create))
            {
                udpAgent.PublishMessage(msgTuple.header, msgTuple.rawMsg);
            }

            m_State = RunStateUntilTransition(registerState);
            Assert.That(m_State, Is.TypeOf<EmitterSynchronization>());

            // Check that we registered all the nodes
            Assert.That(m_Node.RemoteNodes, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestNodeRegistration()
        {
            m_Node.RegisterNode(new RemoteNodeComContext() {ID = 3, Role = NodeRole.Repeater});
            m_Node.RegisterNode(new RemoteNodeComContext() {ID = 5, Role = NodeRole.Repeater});
            Assert.IsTrue(m_Node.UdpAgent.AllNodesMask[3]);
            Assert.IsTrue(m_Node.UdpAgent.AllNodesMask[5]);
            Assert.IsFalse(m_Node.UdpAgent.AllNodesMask[2]);

            Assert.That(m_Node.RemoteNodes, Has.Count.EqualTo(2));
            var nodeIds = m_Node.RemoteNodes.Select(x => x.ID).ToArray();
            var roles = m_Node.RemoteNodes.Select(x => x.Role).ToArray();
            Assert.That(nodeIds, Has.Some.EqualTo(3));
            Assert.That(nodeIds, Has.Some.EqualTo(5));
            Assert.That(roles, Is.All.EqualTo(NodeRole.Repeater));
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronization()
        {
            var emitterSync = new EmitterSynchronization(m_Node);
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext {ID = repeaterId, Role = NodeRole.Repeater});
            }

            using var frameDataBuffer = new FrameDataBuffer(ushort.MaxValue);

            // Make the emitter attach some custom data to the frame.
            byte[] GetCustomData() => Encoding.ASCII.GetBytes($"Hello World {m_Node.CurrentFrameID}");
            int StoreCustomData(NativeArray<byte> buffer)
            {
                var data = GetCustomData();
                buffer.GetSubArray(0, data.Length).CopyFrom(data);
                return data.Length;
            }
            EmitterStateWriter.RegisterOnStoreCustomDataDelegate(42, StoreCustomData);

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Emitter frame ===========
                Assert.AreEqual(m_Node.CurrentFrameID, frameNum);

                // At the beginning of the frame, wait until all nodes have arrived at the new frame
                // This step doesn't wait on Frame 0.
                Assert.IsTrue(RunStateUpdateUntil(emitterSync,
                    state => state.Stage == EmitterSynchronization.EStage.WaitOnRepeatersNextFrame));

                // Next send the current frame data to the repeaters.
                // State should now be ready to proceed.
                Assert.IsTrue(RunStateUntilReadyToProceed(emitterSync));

                // The is the current state data, with custom data appended
                frameDataBuffer.Clear();
                EmitterStateWriter.StoreStateData(frameDataBuffer);
                frameDataBuffer.Store(42, StoreCustomData);
                frameDataBuffer.Store((byte) StateID.End);

                // ======== Nodes receive FrameData packet ======
                foreach (var testAgent in m_TestAgents)
                {
                    var (header, message, frameData) = testAgent.ReceiveMessageWithData<EmitterLastFrameData>();
                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.LastFrameData));
                    Assert.That(message.FrameNumber == frameNum);

                    // Check that state data is received and it corresponds to the current frame.
                    // The received data should also contain custom data at the end.
                    Assert.That(frameData, Is.EqualTo(frameDataBuffer.Data));
                }

                // ======= PostLateUpdate ===========
                // Once repeaters have ACK'ed, emitter should be ready to move onto the next frame
                Assert.IsTrue(RunStateUntilReadyForNextFrame(emitterSync));

                m_Node.EndFrame();
                // ======= End of Frame ===========
                yield return null;
                emitterSync.ProcessFrame(true);

                // ======= Repeaters start new frame =========
                foreach (var (id, agent) in k_RepeaterIds.Zip(m_TestAgents, Tuple.Create))
                {
                    var (header, rawMsg) = GenerateMessage(id,
                        new[]
                        {
                            k_EmitterId
                        },
                        EMessageType.EnterNextFrame,
                        new RepeaterEnteredNextFrame
                        {
                            FrameNumber = frameNum + 1
                        });

                    agent.PublishMessage(header, rawMsg);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronizationHardwareSync()
        {
            m_Node.HasHardwareSync = true;
            var emitterSync = new EmitterSynchronization(m_Node);
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext {ID = repeaterId, Role = NodeRole.Repeater});
            }

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Emitter frame ===========
                Assert.AreEqual(m_Node.CurrentFrameID, frameNum);

                // Emit frame data. Note that we're not waiting for the nodes to signal a new frame.
                Assert.IsTrue(RunStateUntilReadyToProceed(emitterSync));
                Assert.That(emitterSync.Stage, Is.EqualTo(EmitterSynchronization.EStage.WaitForRepeatersToACK));
                // State should now be ready to proceed.

                // ======== Nodes receive FrameData packet ======
                foreach (var testAgent in m_TestAgents)
                {
                    var (header, message, frameData) = testAgent.ReceiveMessageWithData<EmitterLastFrameData>();
                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.LastFrameData));
                    Assert.That(message.FrameNumber == frameNum);
                }

                // ======= PostLateUpdate ===========
                // Once repeaters have ACK'ed, emitter should be ready to move onto the next frame
                Assert.IsTrue(RunStateUntilReadyForNextFrame(emitterSync));

                // ======= End of Frame ===========
                m_Node.EndFrame();
                yield return null;
                emitterSync.ProcessFrame(true);

                // ======= Repeaters start new frame =========
                foreach (var (id, agent) in k_RepeaterIds.Zip(m_TestAgents, Tuple.Create))
                {
                    var (header, rawMsg) = GenerateMessage(id,
                        new[]
                        {
                            k_EmitterId
                        },
                        EMessageType.EnterNextFrame,
                        new RepeaterEnteredNextFrame
                        {
                            FrameNumber = frameNum + 1
                        });

                    agent.PublishMessage(header, rawMsg);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronizationDelayRepeaters()
        {
            m_Node.RepeatersDelayed = true;

            var emitterSync = new EmitterSynchronization(m_Node);
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext {ID = repeaterId, Role = NodeRole.Repeater});
            }

            // Simulate several frames
            const int kNumFrames = 3;
            using var lastFrameDataBuffer = new FrameDataBuffer(ushort.MaxValue);

            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                // ======= Start of Emitter frame =========
                Assert.AreEqual(m_Node.CurrentFrameID, frameNum);

                // The emitter is going to run a frame ahead, so on frame 0 it's going to start right away without
                // waiting for all the nodes to report in.
                if (m_Node.CurrentFrameID == 0)
                {
                    Assert.That(emitterSync.ReadyToProceed, Is.True);
                }

                Assert.IsTrue(RunStateUntilReadyToProceed(emitterSync));
                if (m_Node.CurrentFrameID > 0)
                {
                    // On frame > 0, we proceed after emitting the frame data
                    Assert.That(emitterSync.Stage, Is.EqualTo(EmitterSynchronization.EStage.WaitForRepeatersToACK));
                }
                // State should now be ready to proceed.

                // ======== Nodes receive FrameData packet (maybe) ======
                foreach (var testAgent in m_TestAgents)
                {
                    var (rxHeader, message, frameData) = testAgent.ReceiveMessageWithData<EmitterLastFrameData>(2000);

                    if (m_Node.CurrentFrameID > 0)
                    {
                        // Emitter starts publishing LastFrameData on frame 1
                        Assert.That(rxHeader.MessageType, Is.EqualTo(EMessageType.LastFrameData));

                        // FrameID (frame number) should be behind the emitter by 1
                        Assert.That(message.FrameNumber, Is.EqualTo(m_Node.CurrentFrameID - 1));

                        // Check that the frame state is from the previous frame
                        Assert.That(lastFrameDataBuffer.IsValid);
                        Assert.That(frameData, Is.EqualTo(lastFrameDataBuffer.Data));
                    }
                    else
                    {
                        // Repeaters should not be receiving anything on frame 0
                        Assert.That(rxHeader.MessageType, Is.EqualTo(default(EMessageType)));
                    }
                }

                // Store the state data for the current frame
                lastFrameDataBuffer.Clear();
                EmitterStateWriter.StoreStateData(lastFrameDataBuffer);
                lastFrameDataBuffer.Store((byte) StateID.End);

                // ======= PostLateUpdate ===========
                // Wait for repeaters to ack before starting the next frame.
                // On frame 0, the ack is skipped because it didn't emit frame data.
                if (m_Node.CurrentFrameID > 0)
                {
                    Assert.That(emitterSync.Stage, Is.EqualTo(EmitterSynchronization.EStage.WaitForRepeatersToACK));
                }
                Assert.IsTrue(RunStateUntilReadyForNextFrame(emitterSync));

                // ======= End of Frame ===========
                m_Node.EndFrame();
                yield return null;

                emitterSync.ProcessFrame(true);

                // ======= Repeaters start new frame =========
                foreach (var (id, agent) in k_RepeaterIds.Zip(m_TestAgents, Tuple.Create))
                {
                    var (header, rawMsg) = GenerateMessage(id,
                        new[]
                        {
                            k_EmitterId
                        },
                        EMessageType.EnterNextFrame,
                        new RepeaterEnteredNextFrame
                        {
                            FrameNumber = frameNum  // Repeaters are still on frame N
                        });

                    agent.PublishMessage(header, rawMsg);
                }
            }

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // Awkward way to invoke m_State.ExitState
            new NullState().EnterState(m_State);

            m_Node.Exit();
            foreach (var testAgent in m_TestAgents)
            {
                testAgent.Dispose();
            }
        }
    }
}
