using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncTests
    {
        GameObject m_TestGameObject;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;
        string m_InterfaceName;

        [SetUp]
        public void SetUp()
        {
            // Global state madness!
            // Sneaky scripts can change the command line parser arguments
            // from underneath us. The following lines of code are
            // workarounds to ensure we start the test with a "clean" slate.
            ClusterSync.onPreEnableClusterDisplay = null;
            CommandLineParser.Reset();

            m_InterfaceName = SelectNic().Name;
        }

        UDPAgent GetTestAgent(byte nodeId, int rxPort, int txPort)
        {
            var testConfig = MockClusterSync.udpConfig;
            testConfig.nodeId = nodeId;
            testConfig.rxPort = rxPort;
            testConfig.txPort = txPort;
            testConfig.adapterName = m_InterfaceName;

            return new UDPAgent(testConfig);
        }

        [UnityTest]
        public IEnumerator TestBootstrap()
        {
            // Bootstrap component creates a ClusterDisplayManager then deletes itself
            m_TestGameObject = new GameObject("Bootstrap", typeof(ClusterDisplayBootstrap));
            yield return null;
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayManager>(out _), Is.True);
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayBootstrap>(out _), Is.False);
        }

        [Test]
        [Ignore("ClusterDisplayState is buggy. See comment.")]
        public void TestClusterSyncState()
        {
            // In theory, these properties should be false when there is no active node, but
            // there is no logic to enforce this, so they start off in an indeterminate state.
            Assert.That(ClusterDisplayState.IsActive, Is.False);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator TestClusterSetupEmitter()
        {
            const int numRepeaters = 1;
            var args =
                $"-emitterNode {k_EmitterId} {numRepeaters} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " + 
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} " +
                $"-adapterName {m_InterfaceName}";
            
            CommandLineParser.Reset();

            using var testAgent = GetTestAgent(k_RepeaterId, MockClusterSync.txPort, MockClusterSync.rxPort);
            
            m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));
            
            Assert.That(ClusterDisplayState.IsEmitter, Is.True);
            Assert.That(ClusterDisplayState.IsRepeater, Is.False);
            
            Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(k_EmitterId));
            Assert.That(ClusterDisplayState.IsActive, Is.True);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);
            
            var clusterSync = ClusterSync.Instance;
            var node = clusterSync.LocalNode as EmitterNode;
            
            Assert.That(node, Is.Not.Null);
            Assert.That(node.m_RemoteNodes.Count, Is.Zero);
            Assert.That(node.TotalExpectedRemoteNodesCount, Is.EqualTo(numRepeaters));
            
            var (header, rawMsg) = GenerateMessage(k_RepeaterId,
                new byte[] {k_EmitterId},
                EMessageType.HelloEmitter,
                new RolePublication {NodeRole = ENodeRole.Repeater});
            
            testAgent.PublishMessage(header, rawMsg);
            yield return null;
            Assert.That(node.m_RemoteNodes.Count, Is.EqualTo(numRepeaters));
        }

        [UnityTest]
        public IEnumerator TestClusterSetupRepeater()
        {
            var args =
                $"-node {k_RepeaterId} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " + 
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} " +
                $"-adapterName {m_InterfaceName}";
            
            using var testAgent = GetTestAgent(k_EmitterId, MockClusterSync.txPort, MockClusterSync.rxPort);
            
            CommandLineParser.Reset();
            
            m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));
            
            Assert.That(ClusterDisplayState.IsActive, Is.True);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);
            
            Assert.That(ClusterDisplayState.IsEmitter, Is.False);
            Assert.That(ClusterDisplayState.IsRepeater, Is.True);
            Assert.That(ClusterDisplayState.IsActive, Is.True);
            Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(k_RepeaterId));
            
            var clusterSync = ClusterSync.Instance;

            var node = clusterSync.LocalNode as RepeaterNode;
            Assert.That(node, Is.Not.Null);
            
            var (header, rolePublication) = testAgent.ReceiveMessage<RolePublication>();
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
            Assert.That(rolePublication.NodeRole, Is.EqualTo(ENodeRole.Repeater));
            Assert.That(header.OriginID, Is.EqualTo(k_RepeaterId));
            
            // Send an acceptance message
            testAgent.PublishMessage(new MessageHeader
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = (ulong) 1 << k_RepeaterId,
            });
            
            // Send a GO message
            var (txHeader, lastFrameMsg) = GenerateMessage(k_EmitterId,
                new byte[] {k_RepeaterId},
                EMessageType.LastFrameData,
                new EmitterLastFrameData()
                {
                    FrameNumber = 0
                },
                MessageHeader.EFlag.Broadcast,
                new byte[] {0}); // trailing 0 to indicate empty state data

            testAgent.PublishMessage(txHeader, lastFrameMsg);

            yield return null;
            Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));
        }
        
        [TearDown]
        public void TearDown()
        {   
            if (m_TestGameObject != null)
            {
                Object.Destroy(m_TestGameObject);
            }
        }
    }
}
