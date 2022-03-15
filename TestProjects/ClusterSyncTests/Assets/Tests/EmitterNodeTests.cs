using System;
using System.Collections;
using System.Linq;
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
        const byte k_EmitterId = 0;
        static readonly byte[] k_RepeaterIds = {1, 2};

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
        }
        
        [Test]
        public void TestRegisterRepeaters()
        {
            var registerState = new WaitingForAllClients(m_ClusterSync);
            registerState.EnterState(null);
            
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
            
            Assert.IsTrue(RunStateUntilTransition(registerState) is EmitterSynchronization);
            
            // Check that we registered all the nodes
            var node = m_ClusterSync.LocalNode as EmitterNode;
            Assert.IsNotNull(node);
            Assert.That(node.m_RemoteNodes, Has.Count.EqualTo(2));
            var nodeIds = node.m_RemoteNodes.Select(x => x.ID).ToArray();
            var roles = node.m_RemoteNodes.Select(x => x.Role).ToArray();
            Assert.That(nodeIds, Has.Some.EqualTo(1));
            Assert.That(nodeIds, Has.Some.EqualTo(2));
            Assert.That(roles, Is.All.EqualTo(ENodeRole.Repeater));
        }

        [Test]
        public void TestEmitterSynchronization()
        {
            var emitterSync = new EmitterSynchronization(m_ClusterSync) {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };

            emitterSync.EnterState(null);
        }
        
        [TearDown]
        public void TearDown()
        {
            m_ClusterSync.LocalNode.Exit();
            foreach (var testAgent in m_TestAgents)
            {
                testAgent.Stop();
            }
            
        }
    }
}
