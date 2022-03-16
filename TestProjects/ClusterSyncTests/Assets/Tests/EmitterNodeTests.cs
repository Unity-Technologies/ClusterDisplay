using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.EmitterStateMachine;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class EmitterNodeTests
    {
        MockClusterSync m_ClusterSync;
        UDPAgent[] m_TestAgents;
        EmitterNode m_Node;
        const byte k_EmitterId = 0;
        static readonly byte[] k_RepeaterIds = {1, 2};
        NodeState m_State;

        [SetUp]
        public void SetUp()
        {
            m_ClusterSync = new MockClusterSync(MockClusterSync.NodeType.Emitter, k_EmitterId, numRepeaters: k_RepeaterIds.Length);

            m_TestAgents = k_RepeaterIds.Select(id =>
            {
                var testConfig = MockClusterSync.udpConfig;
                testConfig.nodeId = id;
                testConfig.rxPort = MockClusterSync.udpConfig.txPort;
                testConfig.txPort = MockClusterSync.udpConfig.rxPort;
                return new UDPAgent(testConfig);
            }).ToArray();
            
            m_Node = m_ClusterSync.LocalNode as EmitterNode;
            Assert.IsNotNull(m_Node);
        }
        
        [Test]
        public void TestWaitForClients()
        {
            Assert.That(m_Node.m_RemoteNodes, Is.Empty);
            
            var registerState = new WaitingForAllClients(m_ClusterSync);
            registerState.EnterState(null);

            m_State = registerState;
            
            Assert.AreSame(registerState, registerState.ProcessFrame(false));
            Assert.IsFalse(registerState.ReadyToProceed);

            var helloMsgs = k_RepeaterIds.Select(id => GenerateMessage(id,
                new byte[]{k_EmitterId},
                EMessageType.HelloEmitter,
                new RolePublication{NodeRole = ENodeRole.Repeater}));

            foreach (var (msgTuple, udpAgent) in helloMsgs.Zip(m_TestAgents, Tuple.Create))
            {
                udpAgent.PublishMessage(msgTuple.header, msgTuple.rawMsg);
            }

            m_State = RunStateUntilTransition(registerState);
            Assert.That(m_State, Is.TypeOf<EmitterSynchronization>());
            
            // Check that we registered all the nodes
            Assert.That(m_Node.m_RemoteNodes, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestNodeRegistration()
        {   
            var node = m_ClusterSync.LocalNode as EmitterNode;
            Assert.IsNotNull(node);
            
            node.RegisterNode(new RemoteNodeComContext(){ID = 3, Role = ENodeRole.Repeater});
            node.RegisterNode(new RemoteNodeComContext(){ID = 5, Role = ENodeRole.Repeater});
            Assert.NotZero(node.UdpAgent.AllNodesMask & (1 << 3));
            Assert.NotZero(node.UdpAgent.AllNodesMask & (1 << 5));
            Assert.Zero(node.UdpAgent.AllNodesMask & (1 << 2));
            
            Assert.That(node.m_RemoteNodes, Has.Count.EqualTo(2));
            var nodeIds = node.m_RemoteNodes.Select(x => x.ID).ToArray();
            var roles = node.m_RemoteNodes.Select(x => x.Role).ToArray();
            Assert.That(nodeIds, Has.Some.EqualTo(3));
            Assert.That(nodeIds, Has.Some.EqualTo(5));
            Assert.That(roles, Is.All.EqualTo(ENodeRole.Repeater));
        }

        [UnityTest]
        public IEnumerator TestEmitterSynchronization()
        {
            var emitterSync = new EmitterSynchronization(m_ClusterSync) {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };
            emitterSync.EnterState(null);

            m_State = emitterSync;

            foreach (var repeaterId in k_RepeaterIds)
            {
                m_Node.RegisterNode(new RemoteNodeComContext{ID = repeaterId, Role = ENodeRole.Repeater});
            }

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                m_ClusterSync.CurrentFrameID = frameNum;
                
                // At the beginning of the frame, wait until all nodes have arrived at the new frame
                Assert.That(emitterSync.Stage, Is.EqualTo(EmitterSynchronization.EStage.WaitOnRepeatersNextFrame));

                // Next send the current frame data to the repeaters
                // Note: the name EmitLastFrameData can refer to either the current or the previous frame,
                // depending on whether the repeaters are delayed by one.
                Assert.IsTrue(RunStateUtil(emitterSync,
                    state => state.Stage == EmitterSynchronization.EStage.EmitLastFrameData));
                
                Assert.That(emitterSync.Stage, Is.EqualTo(EmitterSynchronization.EStage.EmitLastFrameData));
                emitterSync.ProcessFrame(false);

                // We should receive a FrameData packet here
                foreach (var testAgent in m_TestAgents)
                {
                    var (header, contents) = testAgent.ReceiveMessage<EmitterLastFrameData>();
                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.LastFrameData));
                }

                // Once repeaters have ACK'ed, should be ready to continue this frame
                Assert.IsTrue(RunStateUntilReady(emitterSync));
                
                yield return null;
                emitterSync.ProcessFrame(true);
                
                // Repeaters send their NextFrame signal
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
        
        [TearDown]
        public void TearDown()
        {
            // Awkward way to invoke m_State.ExitState
            new NullState(m_ClusterSync).EnterState(m_State);
            
            m_Node.Exit();
            foreach (var testAgent in m_TestAgents)
            {
                testAgent.Stop();
            }
        }
    }
}
