using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
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
            CommandLineParser.Override(new List<string>());
            ClusterSync.ClearInstances();

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

        [Test]
        public void TestClusterSetupEmitter()
        {
            const int numRepeaters = 1;
            var argString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var args = argString.Split(" ").ToList();
            args.Add("-adapterName");
            args.Add(m_InterfaceName);

            CommandLineParser.Override(args);

            var emitterClusterSync = ClusterSync.GetUniqueInstance("Emitter");

            ClusterSync.PushInstance("Emitter");
            emitterClusterSync.PrePopulateClusterParams();

            using var testAgent = GetTestAgent(k_RepeaterId, MockClusterSync.txPort, MockClusterSync.rxPort);

            m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));

            var state = (HelloEmitter: 0, CheckRemoteNodes: 1);
            int currentState = 0;

            // Piggy backing off of ClusterSync.OnInnerLoop in order to receive ticks from SystemUpdate while loop.
            ClusterSync.onSyncTick = (TimeSpan elapsed) =>
            {
                if (elapsed.TotalMilliseconds <= 1)
                    return;

                if (currentState == state.HelloEmitter)
                {
                    var node = emitterClusterSync.LocalNode as EmitterNode;

                    Assert.That(ClusterDisplayState.IsEmitter, Is.True);
                    Assert.That(ClusterDisplayState.IsRepeater, Is.False);

                    Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(k_EmitterId));
                    Assert.That(ClusterDisplayState.IsActive, Is.True);
                    Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);

                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.m_RemoteNodes.Count, Is.Zero);
                    Assert.That(node.TotalExpectedRemoteNodesCount, Is.EqualTo(numRepeaters));

                    var (header, rawMsg) = GenerateMessage(k_RepeaterId,
                        new byte[] {k_EmitterId},
                        EMessageType.HelloEmitter,
                        new RolePublication {NodeRole = ENodeRole.Repeater});

                    testAgent.PublishMessage(header, rawMsg);
                }

                else if (currentState == state.CheckRemoteNodes)
                {
                    var node = emitterClusterSync.LocalNode as EmitterNode;
                    Assert.That(node.m_RemoteNodes.Count, Is.EqualTo(numRepeaters));
                }

                currentState++;
            };
        }

        [Test]
        public void TestClusterSetupRepeater()
        {
            var argString =
                $"-node {k_RepeaterId} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var args = argString.Split(" ").ToList();
            args.Add("-adapterName");
            args.Add(m_InterfaceName);

            CommandLineParser.Override(args);
            var repeaterClusterSync = ClusterSync.GetUniqueInstance("Repeater");
            ClusterSync.PushInstance("Repeater");
            repeaterClusterSync.PrePopulateClusterParams();

            using var testAgent = GetTestAgent(k_EmitterId, MockClusterSync.txPort, MockClusterSync.rxPort);

            m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));

            Assert.That(ClusterDisplayState.IsActive, Is.True);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);

            Assert.That(ClusterDisplayState.IsEmitter, Is.False);
            Assert.That(ClusterDisplayState.IsRepeater, Is.True);
            Assert.That(ClusterDisplayState.IsActive, Is.True);
            Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(k_RepeaterId));

            var node = repeaterClusterSync.LocalNode as RepeaterNode;
            Assert.That(node, Is.Not.Null);

            var state = (HelloEmitter: 0, WelcomeRepeater: 1, LastFrameData: 2);
            int currentState = 0;

            // Piggy backing off of ClusterSync.OnInnerLoop in order to receive ticks from SystemUpdate while loop.
            ClusterSync.onSyncTick = (TimeSpan elapsed) =>
            {
                if (elapsed.TotalMilliseconds <= 5)
                    return;

                if (currentState == state.HelloEmitter)
                {
                    var (header, rolePublication) = testAgent.ReceiveMessage<RolePublication>();
                    if (header.MessageType == EMessageType.AckMsgRx)
                        return;

                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
                    Assert.That(rolePublication.NodeRole, Is.EqualTo(ENodeRole.Repeater));
                    Assert.That(header.OriginID, Is.EqualTo(k_RepeaterId));
                }

                else if (currentState == state.WelcomeRepeater)
                {
                    // Send an acceptance message
                    testAgent.PublishMessage(new MessageHeader
                    {
                        MessageType = EMessageType.WelcomeRepeater,
                        DestinationIDs = BitVector.FromIndex(k_RepeaterId),
                    });
                }

                else if (currentState == state.LastFrameData)
                {
                    Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));

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
                }

                // Step to the next state.
                currentState++;
            };
        }

        [Test]
        public void EmitterAndRepeaterLockstepFor10Frames()
        {
            const int numRepeaters = 1;
            var repeaterArgsString =
                $"-node {k_RepeaterId} {MockClusterSync.multicastAddress}:{MockClusterSync.txPort},{MockClusterSync.rxPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";
            
            var repeaterArgs = repeaterArgsString.Split(" ").ToList();
            repeaterArgs.Add("-adapterName");
            repeaterArgs.Add(m_InterfaceName);

            var emitterArgsString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var emitterArgs = emitterArgsString.Split(" ").ToList();
            emitterArgs.Add("-adapterName");
            emitterArgs.Add(m_InterfaceName);

            var repeaterClusterSync = ClusterSync.GetUniqueInstance("Repeater");
            var emitterClusterSync = ClusterSync.GetUniqueInstance("Emitter");

            ClusterSync.PushInstance("Repeater");
            CommandLineParser.Override(repeaterArgs);
            repeaterClusterSync.PrePopulateClusterParams();

            ClusterSync.PushInstance("Emitter");
            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.PrePopulateClusterParams();

            m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));

            ClusterSync.onSyncTick = (TimeSpan elapsed) =>
            {
                ClusterSync.PushInstance("Repeater");
                Assert.That(ClusterDisplayState.IsActive, Is.True);
                Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);

                Assert.That(ClusterDisplayState.IsEmitter, Is.False);
                Assert.That(ClusterDisplayState.IsRepeater, Is.True);
                Assert.That(ClusterDisplayState.IsActive, Is.True);
                Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(k_RepeaterId));
                ClusterSync.PopInstance();

                ClusterSync.PushInstance("Emitter");
                Assert.That(ClusterDisplayState.IsActive, Is.True);
                Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.True);

                Assert.That(ClusterDisplayState.IsEmitter, Is.True);
                Assert.That(ClusterDisplayState.IsRepeater, Is.False);
                Assert.That(ClusterDisplayState.IsActive, Is.True);
                Assert.That(ClusterDisplayState.NodeID, Is.EqualTo(0));
                ClusterSync.PopInstance();
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (m_TestGameObject != null)
            {
                Object.Destroy(m_TestGameObject);
            }

            if (ClusterSync.InstanceExists("Emitter"))
            {
                ClusterSync.PushInstance("Emitter");
                ClusterSync.Instance.ShutdownAllClusterNodes();
            }

            if (ClusterSync.InstanceExists("Repeater"))
            {
                ClusterSync.PushInstance("Repeater");
                ClusterSync.Instance.ShutdownAllClusterNodes();
            }

            ClusterSync.ClearInstances();
        }
    }
}
