using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using System.Diagnostics;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncTests
    {
        GameObject m_TestGameObject;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;
        string m_InterfaceName;

        readonly List<ClusterSync> m_Instances = new List<ClusterSync>();

        [SetUp]
        public void SetUp()
        {
            // Global state madness!
            // Sneaky scripts can change the command line parser arguments
            // from underneath us. The following lines of code are
            // workarounds to ensure we start the test with a "clean" slate.
            ClusterSync.onPreEnableClusterDisplay = null;
            CommandLineParser.Override(new List<string>());

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
            var emitterArgsString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var emitterArgs = emitterArgsString.Split(" ").ToList();
            emitterArgs.Add("-adapterName");
            emitterArgs.Add(m_InterfaceName);

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.PrePopulateClusterParams();
            emitterClusterSync.EnableClusterDisplay();

            using var testAgent = GetTestAgent(k_RepeaterId, MockClusterSync.txPort, MockClusterSync.rxPort);

            ClusterSyncLooper.OnInstanceTick onInstanceTick = null;
            onInstanceTick = (ClusterSyncLooper.TickType tickType) =>
            {
                if (tickType == ClusterSyncLooper.TickType.DoFrame)
                {
                    var node = emitterClusterSync.LocalNode as EmitterNode;

                    Assert.That(emitterClusterSync.state.IsEmitter, Is.True);
                    Assert.That(emitterClusterSync.state.IsRepeater, Is.False);

                    Assert.That(emitterClusterSync.state.NodeID, Is.EqualTo(k_EmitterId));
                    Assert.That(emitterClusterSync.state.IsActive, Is.True);
                    Assert.That(emitterClusterSync.state.IsClusterLogicEnabled, Is.True);

                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.m_RemoteNodes.Count, Is.Zero);
                    Assert.That(node.TotalExpectedRemoteNodesCount, Is.EqualTo(numRepeaters));

                    var (header, rawMsg) = GenerateMessage(k_RepeaterId,
                        new byte[] {k_EmitterId},
                        EMessageType.HelloEmitter,
                        new RolePublication {NodeRole = ENodeRole.Repeater});

                    testAgent.PublishMessage(header, rawMsg);
                    ClusterSyncLooper.onInstanceTick -= onInstanceTick;
                }
            };

            ClusterSyncLooper.onInstanceTick = onInstanceTick;

            var node = emitterClusterSync.LocalNode as EmitterNode;
            while (node.m_RemoteNodes.Count != numRepeaters)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TestClusterSetupRepeater()
        {
            var argString =
                $"-node {k_RepeaterId} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var args = argString.Split(" ").ToList();
            args.Add("-adapterName");
            args.Add(m_InterfaceName);

            var repeaterClusterSync = new ClusterSync("Repeater");
            m_Instances.Add(repeaterClusterSync);

            CommandLineParser.Override(args);
            repeaterClusterSync.PrePopulateClusterParams();
            repeaterClusterSync.EnableClusterDisplay();

            using var testAgent = GetTestAgent(k_EmitterId, MockClusterSync.txPort, MockClusterSync.rxPort);

            Assert.That(repeaterClusterSync.state.IsActive, Is.True);
            Assert.That(repeaterClusterSync.state.IsClusterLogicEnabled, Is.True);

            Assert.That(repeaterClusterSync.state.IsEmitter, Is.False);
            Assert.That(repeaterClusterSync.state.IsRepeater, Is.True);
            Assert.That(repeaterClusterSync.state.IsActive, Is.True);
            Assert.That(repeaterClusterSync.state.NodeID, Is.EqualTo(k_RepeaterId));

            var node = repeaterClusterSync.LocalNode as RepeaterNode;
            Assert.That(node, Is.Not.Null);

            var step = (HelloEmitter: 0, WelcomeRepeater: 1, LastFrameData: 2);
            int currentStep = 0;

            // Piggy backing off of ClusterSync.OnInnerLoop in order to receive ticks from SystemUpdate while loop.
            ClusterSyncLooper.OnInstanceTick onInstanceTick = null;
            onInstanceTick = (ClusterSyncLooper.TickType tickType) =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                if (currentStep == step.HelloEmitter)
                {
                    var (header, rolePublication) = testAgent.ReceiveMessage<RolePublication>();
                    if (header.MessageType == EMessageType.AckMsgRx)
                    {
                        return;
                    }

                    Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
                    Assert.That(rolePublication.NodeRole, Is.EqualTo(ENodeRole.Repeater));
                    Assert.That(header.OriginID, Is.EqualTo(k_RepeaterId));
                }

                else if (currentStep == step.WelcomeRepeater)
                {
                    // Send an acceptance message
                    testAgent.PublishMessage(new MessageHeader
                    {
                        MessageType = EMessageType.WelcomeRepeater,
                        DestinationIDs = BitVector.FromIndex(k_RepeaterId),
                    });
                }

                else if (currentStep == step.LastFrameData)
                {
                    Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));

                    // Send a GO message
                    var (txHeader, lastFrameMsg) = GenerateMessage(k_EmitterId,
                        new byte[] {k_RepeaterId},
                        EMessageType.LastFrameData,
                        new EmitterLastFrameData() { FrameNumber = 0 },
                        MessageHeader.EFlag.Broadcast,
                        new byte[] {0}); // trailing 0 to indicate empty state data

                    testAgent.PublishMessage(txHeader, lastFrameMsg);
					Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));
					
                    ClusterSyncLooper.onInstanceTick -= onInstanceTick;
                }

                // Step to the next state.
                currentStep++;
            };

            ClusterSyncLooper.onInstanceTick = onInstanceTick;
            while (currentStep < step.LastFrameData)
                yield return null;
        }

        [UnityTest]
        public IEnumerator EmitterThenRepeaterLockstepFor10Frames()
        {
            const int numRepeaters = 1;

            var emitterArgsString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {MockClusterSync.multicastAddress}:{MockClusterSync.rxPort},{MockClusterSync.txPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";

            var emitterArgs = emitterArgsString.Split(" ").ToList();
            emitterArgs.Add("-adapterName");
            emitterArgs.Add(m_InterfaceName);

            var repeaterArgsString =
                $"-node {k_RepeaterId} {MockClusterSync.multicastAddress}:{MockClusterSync.txPort},{MockClusterSync.rxPort} " +
                $"-handshakeTimeout {MockClusterSync.timeoutSeconds * 1000} ";
            
            var repeaterArgs = repeaterArgsString.Split(" ").ToList();
            repeaterArgs.Add("-adapterName");
            repeaterArgs.Add(m_InterfaceName);

            var emitterClusterSync = new ClusterSync("Emitter");
            var repeaterClusterSync = new ClusterSync("Repeater");

            m_Instances.Add(emitterClusterSync);
            m_Instances.Add(repeaterClusterSync);

            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.PrePopulateClusterParams();
            emitterClusterSync.EnableClusterDisplay();

            CommandLineParser.Override(repeaterArgs);
            repeaterClusterSync.PrePopulateClusterParams();
            repeaterClusterSync.EnableClusterDisplay();

            // m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));

            ClusterSyncLooper.onInstanceTick = (ClusterSyncLooper.TickType tickType) =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                Assert.That(emitterClusterSync.state.IsActive, Is.True);
                Assert.That(emitterClusterSync.state.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.state.IsEmitter, Is.True);
                Assert.That(emitterClusterSync.state.IsRepeater, Is.False);
                Assert.That(emitterClusterSync.state.IsActive, Is.True);
                Assert.That(emitterClusterSync.state.NodeID, Is.EqualTo(0));

                Assert.That(repeaterClusterSync.state.IsActive, Is.True);
                Assert.That(repeaterClusterSync.state.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.state.IsEmitter, Is.False);
                Assert.That(repeaterClusterSync.state.IsRepeater, Is.True);
                Assert.That(repeaterClusterSync.state.IsActive, Is.True);
                Assert.That(repeaterClusterSync.state.NodeID, Is.EqualTo(k_RepeaterId));
            };

            while (emitterClusterSync.CurrentFrameID < 10 && repeaterClusterSync.CurrentFrameID < 10)
                yield return null;
        }

        [UnityTest]
        public IEnumerator RepeaterThenEmitterLockstepFor10Frames()
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

            var repeaterClusterSync = new ClusterSync("Repeater");
            var emitterClusterSync = new ClusterSync("Emitter");

            m_Instances.Add(repeaterClusterSync);
            m_Instances.Add(emitterClusterSync);

            CommandLineParser.Override(repeaterArgs);
            repeaterClusterSync.PrePopulateClusterParams();
            repeaterClusterSync.EnableClusterDisplay();

            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.PrePopulateClusterParams();
            emitterClusterSync.EnableClusterDisplay();

            // m_TestGameObject = new GameObject("Manager", typeof(ClusterDisplayManager));

            ClusterSyncLooper.onInstanceTick = (ClusterSyncLooper.TickType tickType) =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                Assert.That(repeaterClusterSync.state.IsActive, Is.True);
                Assert.That(repeaterClusterSync.state.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.state.IsEmitter, Is.False);
                Assert.That(repeaterClusterSync.state.IsRepeater, Is.True);
                Assert.That(repeaterClusterSync.state.IsActive, Is.True);
                Assert.That(repeaterClusterSync.state.NodeID, Is.EqualTo(k_RepeaterId));

                Assert.That(emitterClusterSync.state.IsActive, Is.True);
                Assert.That(emitterClusterSync.state.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.state.IsEmitter, Is.True);
                Assert.That(emitterClusterSync.state.IsRepeater, Is.False);
                Assert.That(emitterClusterSync.state.IsActive, Is.True);
                Assert.That(emitterClusterSync.state.NodeID, Is.EqualTo(0));
            };

            while (emitterClusterSync.CurrentFrameID < 10 && repeaterClusterSync.CurrentFrameID < 10)
                yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_TestGameObject != null)
            {
                Object.Destroy(m_TestGameObject);
            }

            foreach (var instance in m_Instances)
            {
                instance.ShutdownAllClusterNodes();
                instance.CleanUp();
            }

            m_Instances.Clear();
        }
    }
}
