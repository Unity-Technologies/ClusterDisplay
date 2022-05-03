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
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class ClusterSyncTests
    {
        GameObject m_TestGameObject;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;
        string m_InterfaceName;

        readonly List<ClusterSync> m_Instances = new List<ClusterSync>();
        UDPAgent m_TestAgent;
        Action<ClusterSyncLooper.TickType> m_TickHandler;

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
            var testConfig = udpConfig;
            testConfig.nodeId = nodeId;
            testConfig.rxPort = rxPort;
            testConfig.txPort = txPort;
            testConfig.adapterName = m_InterfaceName;

            return new UDPAgent(testConfig);
        }

        [UnityTest]
        public IEnumerator TestBootstrap()
        {
            // To make this test not freeze, we need to initialize the params so that
            // cluster logic is disabled
            CommandLineParser.Override(new List<string>());

            // Bootstrap component creates a ClusterDisplayManager then deletes itself
            m_TestGameObject = new GameObject("Bootstrap", typeof(ClusterDisplayBootstrap));
            yield return null;
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayManager>(out _), Is.True);
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayBootstrap>(out _), Is.False);

            Assert.That(ClusterDisplayManager.ClusterSyncInstance, Is.EqualTo(ServiceLocator.Get<ClusterSync>()));
            m_Instances.Add(ClusterDisplayManager.ClusterSyncInstance);
        }

        [Test]
        public void TestClusterSyncState()
        {
            ServiceLocator.Provide(new ClusterSync());
            Assert.That(ClusterDisplayState.IsActive, Is.False);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.False);
            m_Instances.Add(ServiceLocator.Get<ClusterSync>());
        }

        [UnityTest]
        public IEnumerator TestClusterSetupEmitter()
        {
            const int numRepeaters = 1;
            var emitterArgsString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {multicastAddress}:{rxPort},{txPort} " +
                $"-handshakeTimeout {timeoutSeconds * 1000} ";

            var emitterArgs = emitterArgsString.Split(" ").ToList();
            emitterArgs.Add("-adapterName");
            emitterArgs.Add(m_InterfaceName);

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.ReadParamsFromCommandLine();
            emitterClusterSync.EnableClusterDisplay();
            m_TestAgent = GetTestAgent(k_RepeaterId, NodeTestUtils.txPort, NodeTestUtils.rxPort);

            m_TickHandler = tickType =>
            {
                if (tickType == ClusterSyncLooper.TickType.DoFrame)
                {
                    using var testAgent = GetTestAgent(k_RepeaterId, txPort, rxPort);

                    var node = emitterClusterSync.LocalNode as EmitterNode;

                    Assert.That(emitterClusterSync.StateAccessor.IsEmitter, Is.True);
                    Assert.That(emitterClusterSync.StateAccessor.IsRepeater, Is.False);

                    Assert.That(emitterClusterSync.StateAccessor.NodeID, Is.EqualTo(k_EmitterId));
                    Assert.That(emitterClusterSync.StateAccessor.IsActive, Is.True);
                    Assert.That(emitterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);

                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.RemoteNodes.Count, Is.Zero);
                    Assert.That(node.TotalExpectedRemoteNodesCount, Is.EqualTo(numRepeaters));

                    var (header, rawMsg) = GenerateMessage(k_RepeaterId, new[] {k_EmitterId},
                        EMessageType.HelloEmitter, new RolePublication {NodeRole = ENodeRole.Repeater});

                    m_TestAgent.PublishMessage(header, rawMsg);
                    ClusterSyncLooper.onInstanceTick -= m_TickHandler;
                }
            };

            ClusterSyncLooper.onInstanceTick += m_TickHandler;

            var node = emitterClusterSync.LocalNode as EmitterNode;
            Assert.That(node, Is.Not.Null);
            while (node.RemoteNodes.Count != numRepeaters)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TestClusterSetupRepeater()
        {
            var argString =
                $"-node {k_RepeaterId} {multicastAddress}:{rxPort},{txPort} " +
                $"-handshakeTimeout {timeoutSeconds * 1000} ";

            var args = argString.Split(" ").ToList();
            args.Add("-adapterName");
            args.Add(m_InterfaceName);

            var repeaterClusterSync = new ClusterSync("Repeater");
            m_Instances.Add(repeaterClusterSync);

            CommandLineParser.Override(args);
            repeaterClusterSync.ReadParamsFromCommandLine();
            repeaterClusterSync.EnableClusterDisplay();

            m_TestAgent = GetTestAgent(k_EmitterId, NodeTestUtils.txPort, NodeTestUtils.rxPort);

            Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
            Assert.That(repeaterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);

            Assert.That(repeaterClusterSync.StateAccessor.IsEmitter, Is.False);
            Assert.That(repeaterClusterSync.StateAccessor.IsRepeater, Is.True);
            Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
            Assert.That(repeaterClusterSync.StateAccessor.NodeID, Is.EqualTo(k_RepeaterId));

            var node = repeaterClusterSync.LocalNode as RepeaterNode;
            Assert.That(node, Is.Not.Null);

            var step = (HelloEmitter: 0, WelcomeRepeater: 1, LastFrameData: 2);
            int currentStep = 0;

            // Piggy backing off of ClusterSync.OnInnerLoop in order to receive ticks from SystemUpdate while loop.
            m_TickHandler = tickType =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                if (currentStep == step.HelloEmitter)
                {
                    var (header, rolePublication) = m_TestAgent.ReceiveMessage<RolePublication>();
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
                    m_TestAgent.PublishMessage(new MessageHeader {MessageType = EMessageType.WelcomeRepeater, DestinationIDs = BitVector.FromIndex(k_RepeaterId),});
                }

                else if (currentStep == step.LastFrameData)
                {
                    Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));

                    // Send a GO message
                    var (txHeader, lastFrameMsg) = GenerateMessage(k_EmitterId, new byte[] {k_RepeaterId}, EMessageType.LastFrameData, new EmitterLastFrameData() {FrameNumber = 0}, MessageHeader.EFlag.Broadcast, Enumerable.Repeat((byte)0, 32).ToArray()); // trailing 0s to indicate empty state data

                    m_TestAgent.PublishMessage(txHeader, lastFrameMsg);
                    Assert.That(node.EmitterNodeId, Is.EqualTo(k_EmitterId));

                    ClusterSyncLooper.onInstanceTick -= m_TickHandler;
                }

                // Step to the next state.
                currentStep++;
            };

            ClusterSyncLooper.onInstanceTick += m_TickHandler;
            while (currentStep < step.LastFrameData)
                yield return null;
        }

        private ClusterSync CreateEmitter ()
        {
            const int numRepeaters = 1;

            var emitterArgsString =
                $"-emitterNode {k_EmitterId} {numRepeaters} {NodeTestUtils.multicastAddress}:{NodeTestUtils.rxPort},{NodeTestUtils.txPort} " +
                $"-handshakeTimeout {NodeTestUtils.timeoutSeconds * 1000} ";

            var emitterArgs = emitterArgsString.Split(" ").ToList();
            emitterArgs.Add("-adapterName");
            emitterArgs.Add(m_InterfaceName);

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            CommandLineParser.Override(emitterArgs);
            emitterClusterSync.ReadParamsFromCommandLine();
            emitterClusterSync.EnableClusterDisplay();
            return emitterClusterSync;
        }

        private ClusterSync CreateRepeater ()
        {
            var repeaterArgsString =
                $"-node {k_RepeaterId} {NodeTestUtils.multicastAddress}:{NodeTestUtils.txPort},{NodeTestUtils.rxPort} " +
                $"-handshakeTimeout {NodeTestUtils.timeoutSeconds * 1000} ";

            var repeaterArgs = repeaterArgsString.Split(" ").ToList();
            repeaterArgs.Add("-adapterName");
            repeaterArgs.Add(m_InterfaceName);

            var repeaterClusterSync = new ClusterSync("Repeater");

            m_Instances.Add(repeaterClusterSync);

            CommandLineParser.Override(repeaterArgs);
            repeaterClusterSync.ReadParamsFromCommandLine();
            repeaterClusterSync.EnableClusterDisplay();
            return repeaterClusterSync;
        }

        /// <summary>
        ///  This test FIRST initializes a EMITTER ClusterSync, then a REPEATER
        ///  ClusterSync. Therefore, the emitter is the first delegate registered
        ///  in ClusterSyncLooper. This demonstrates that the we can change the
        ///  order with SystemUpdate and everything works.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator EmitterThenRepeaterLockstepFor10Frames()
        {
            var emitterClusterSync = CreateEmitter();
            var repeaterClusterSync = CreateRepeater();

            // Piggy back on SystemUpdate in order to validate state for both the emitter and repeater.
            m_TickHandler = tickType =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                Assert.That(emitterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsEmitter, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsRepeater, Is.False);
                Assert.That(emitterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.NodeID, Is.EqualTo(0));

                Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsEmitter, Is.False);
                Assert.That(repeaterClusterSync.StateAccessor.IsRepeater, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.NodeID, Is.EqualTo(k_RepeaterId));
            };

            ClusterSyncLooper.onInstanceTick += m_TickHandler;

            while (emitterClusterSync.CurrentFrameID < 10 && repeaterClusterSync.CurrentFrameID < 10)
                yield return null;

            ClusterSyncLooper.onInstanceTick -= m_TickHandler;
        }

        /// <summary>
        ///  This test FIRST initializes a REPEATER ClusterSync, then a EMITTER
        ///  ClusterSync. Therefore, the repeater is the first delegate registered
        ///  in ClusterSyncLooper. This demonstrates that the we can change the
        ///  order with SystemUpdate and everything works.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator RepeaterThenEmitterLockstepFor10Frames()
        {
            var repeaterClusterSync = CreateRepeater();
            var emitterClusterSync = CreateEmitter();

            // Piggy back on SystemUpdate in order to validate state for both the emitter and repeater.
            m_TickHandler = tickType =>
            {
                if (tickType != ClusterSyncLooper.TickType.DoFrame)
                {
                    return;
                }

                Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsEmitter, Is.False);
                Assert.That(repeaterClusterSync.StateAccessor.IsRepeater, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(repeaterClusterSync.StateAccessor.NodeID, Is.EqualTo(k_RepeaterId));

                Assert.That(emitterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsEmitter, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.IsRepeater, Is.False);
                Assert.That(emitterClusterSync.StateAccessor.IsActive, Is.True);
                Assert.That(emitterClusterSync.StateAccessor.NodeID, Is.EqualTo(0));
            };

            ClusterSyncLooper.onInstanceTick += m_TickHandler;

            while (emitterClusterSync.CurrentFrameID < 10 && repeaterClusterSync.CurrentFrameID < 10)
                yield return null;

            ClusterSyncLooper.onInstanceTick -= m_TickHandler;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_TickHandler != null)
            {
                ClusterSyncLooper.onInstanceTick -= m_TickHandler;
            }

            if (m_TestGameObject != null)
            {
                Object.Destroy(m_TestGameObject);
            }

            foreach (var instance in m_Instances)
            {
                instance.DisableClusterDisplay();
            }

            m_TestAgent?.Dispose();

            m_Instances.Clear();
        }
    }
}
