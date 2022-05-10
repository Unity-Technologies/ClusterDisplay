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
            ServiceLocator.Provide<IClusterSyncState>(null);
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
            // Bootstrap component creates a ClusterDisplayManager then deletes itself
            // ClusterDisplayManager uses commandline params.
            // This is a hack to make the ClusterDisplayManager initialize with
            // cluster logic disabled.
            CommandLineParser.Override(new List<string>());
            m_TestGameObject = new GameObject("Bootstrap", typeof(ClusterDisplayBootstrap));
            yield return null;
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayManager>(out _), Is.True);
            Assert.That(m_TestGameObject.TryGetComponent<ClusterDisplayBootstrap>(out _), Is.False);

            Assert.That(ClusterDisplayManager.ClusterSyncInstance, Is.EqualTo(ServiceLocator.Get<IClusterSyncState>()));
            m_Instances.Add(ClusterDisplayManager.ClusterSyncInstance);
        }

        [Test]
        public void TestClusterDisplayState()
        {
            Assert.That(ClusterDisplayState.IsActive, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = ClusterDisplayState.NodeID);

            var clusterSync = new ClusterSync();
            ServiceLocator.Provide<IClusterSyncState>(clusterSync);
            Assert.That(ClusterDisplayState.IsClusterLogicEnabled, Is.False);
            m_Instances.Add(clusterSync);
        }

        [UnityTest]
        public IEnumerator TestClusterSetupEmitter()
        {
            const int numRepeaters = 1;

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            emitterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = true,
                NodeID = k_EmitterId,
                RepeaterCount = numRepeaters,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                RXPort = RxPort,
                TXPort = TxPort,
                CommunicationTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds),
                HandshakeTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds)
            });
            m_TestAgent = GetTestAgent(k_RepeaterId, TxPort, RxPort);

            m_TickHandler = tickType =>
            {
                if (tickType == ClusterSyncLooper.TickType.DoFrame)
                {
                    using var testAgent = GetTestAgent(k_RepeaterId, TxPort, RxPort);

                    var node = emitterClusterSync.LocalNode as EmitterNode;

                    Assert.That(emitterClusterSync.NodeRole, Is.EqualTo(NodeRole.Emitter));

                    Assert.That(emitterClusterSync.NodeID, Is.EqualTo(k_EmitterId));
                    Assert.That(emitterClusterSync.IsClusterLogicEnabled, Is.True);

                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.RemoteNodes.Count, Is.Zero);
                    Assert.That(node.TotalExpectedRemoteNodesCount, Is.EqualTo(numRepeaters));

                    var (header, rawMsg) = GenerateMessage(k_RepeaterId, new[] {k_EmitterId},
                        EMessageType.HelloEmitter, new RolePublication {NodeRole = NodeRole.Repeater});

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
            var repeaterClusterSync = new ClusterSync("Repeater");
            m_Instances.Add(repeaterClusterSync);

            repeaterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = false,
                NodeID = k_RepeaterId,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                RXPort = RxPort,
                TXPort = TxPort,
                CommunicationTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds),
                HandshakeTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds)
            });

            m_TestAgent = GetTestAgent(k_EmitterId, TxPort, RxPort);

            Assert.That(repeaterClusterSync.IsClusterLogicEnabled, Is.True);

            Assert.That(repeaterClusterSync.NodeRole, Is.EqualTo(NodeRole.Repeater));
            Assert.That(repeaterClusterSync.NodeID, Is.EqualTo(k_RepeaterId));

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
                    Assert.That(rolePublication.NodeRole, Is.EqualTo(NodeRole.Repeater));
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

            var emitterClusterSync = new ClusterSync("Emitter");
            m_Instances.Add(emitterClusterSync);

            emitterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = true,
                NodeID = k_EmitterId,
                RepeaterCount = numRepeaters,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                RXPort = RxPort,
                TXPort = TxPort,
                CommunicationTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds),
                HandshakeTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds)
            });
            return emitterClusterSync;
        }

        private ClusterSync CreateRepeater ()
        {
            var repeaterClusterSync = new ClusterSync("Repeater");

            m_Instances.Add(repeaterClusterSync);

            repeaterClusterSync.EnableClusterDisplay(new ClusterParams
            {
                ClusterLogicSpecified = true,
                EmitterSpecified = false,
                NodeID = k_RepeaterId,
                MulticastAddress = MulticastAddress,
                AdapterName = m_InterfaceName,
                RXPort = TxPort,
                TXPort = RxPort,
                CommunicationTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds),
                HandshakeTimeout = new TimeSpan(0, 0, 0, TimeoutSeconds)
            });
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

                Assert.That(emitterClusterSync.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.NodeRole, Is.EqualTo(NodeRole.Emitter));
                Assert.That(emitterClusterSync.NodeID, Is.EqualTo(0));

                Assert.That(repeaterClusterSync.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.NodeRole, Is.EqualTo(NodeRole.Repeater));
                Assert.That(repeaterClusterSync.NodeID, Is.EqualTo(k_RepeaterId));
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

                Assert.That(repeaterClusterSync.IsClusterLogicEnabled, Is.True);
                Assert.That(repeaterClusterSync.NodeRole, Is.EqualTo(NodeRole.Repeater));
                Assert.That(repeaterClusterSync.NodeID, Is.EqualTo(k_RepeaterId));

                Assert.That(emitterClusterSync.IsClusterLogicEnabled, Is.True);
                Assert.That(emitterClusterSync.NodeRole, Is.EqualTo(NodeRole.Emitter));
                Assert.That(emitterClusterSync.NodeID, Is.EqualTo(0));
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
