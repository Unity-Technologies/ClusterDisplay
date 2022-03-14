using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay.RepeaterStateMachine;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.Utils;

namespace Unity.ClusterDisplay.Tests
{
    class MockClusterSync : IClusterSyncState
    {
        public const byte nodeId = 1;
        public const int rxPort = 12345;
        public const int txPort = 12346;
        public const string multicastAddress = "224.0.1.0";
        public const int timeoutSeconds = 10;
        public const int maxRetries = 20;

        public static readonly string adapterName = SelectNic().Name;
        
        public static readonly UDPAgent.Config udpConfig = new UDPAgent.Config
        {
            nodeId = nodeId,
            ip = multicastAddress,
            rxPort = rxPort,
            txPort = txPort,
            timeOut = timeoutSeconds,
            adapterName = adapterName
        };

        public MockClusterSync()
        {
            LocalNode = new MockRepeaterNode(this, false, udpConfig);
        }

        public ulong CurrentFrameID { get; } = 0;
        public ClusterNode LocalNode { get; }

        // A node state placeholder
        class NullState : NodeState
        {
            public NullState(IClusterSyncState clusterSync)
                : base(clusterSync) { }
        }

        // A Repeater node that doesn't do anything
        class MockRepeaterNode : RepeaterNode
        {
            public MockRepeaterNode(IClusterSyncState clusterSync, bool delayRepeater, UDPAgent.Config config)
                : base(clusterSync, delayRepeater, config)
            {
                var oldState = m_CurrentState;
                m_CurrentState = new NullState(clusterSync);
                m_CurrentState.EnterState(oldState);
            }
        }
    }

    public class RepeaterNodeTests
    {
        MockClusterSync m_ClusterSync;
        UDPAgent m_TestAgent;

        [SetUp]
        public void SetUp()
        {
            m_ClusterSync = new MockClusterSync();

            var testConfig = MockClusterSync.udpConfig;
            testConfig.nodeId = 0;
            testConfig.rxPort = MockClusterSync.udpConfig.txPort;
            testConfig.txPort = MockClusterSync.udpConfig.rxPort;
            
            m_TestAgent = new UDPAgent(testConfig);
        }

        [UnityTest]
        public IEnumerator TestRegisterWithEmitterAsync()
        {
            return TestAsyncTask(TestRegisterWithEmitter(), MockClusterSync.timeoutSeconds);
        }

        async Task TestRegisterWithEmitter()
        {
            // Create the state under test
            var registerState = new RegisterWithEmitter(m_ClusterSync)
            {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };

            // Before receiving a WelcomeRepeater, we should be staying in this state
            registerState.EnterState(null);
            Assert.That(registerState.ProcessFrame(true), Is.EqualTo(registerState));

            // The state should be broadcasting HelloEmitter messages
            var (header, rolePublication) = await m_TestAgent.ReceiveMessage<RolePublication>();
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
            Assert.That(rolePublication.NodeRole == ENodeRole.Repeater);

            // ReadyToProceed should be false. This state does not allow advancement of frames
            Assert.False(registerState.ReadyToProceed);

            // Send an acceptance message
            m_TestAgent.PublishMessage(new MessageHeader
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = (ulong)1 << header.OriginID,
            });

            // Wait for the state to transition
            var canExitState = false;
            for (var i = 0; i < MockClusterSync.maxRetries; i++)
            {
                await Task.Delay(100);
                if (registerState.ProcessFrame(true) is RepeaterSynchronization)
                {
                    canExitState = true;
                    break;
                }
            }

            Assert.True(canExitState);
        }

        [TearDown]
        public void TearDown()
        {
            m_ClusterSync.LocalNode.Exit();
            m_TestAgent.Stop();
        }
    }
}
