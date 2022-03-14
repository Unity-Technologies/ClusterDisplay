using System;

namespace Unity.ClusterDisplay.Tests
{
    class MockClusterSync : IClusterSyncState
    {
        public enum NodeType
        {
            Emitter,
            Repeater
        }
        
        public const byte nodeId = 1;
        public const int rxPort = 12345;
        public const int txPort = 12346;
        public const string multicastAddress = "224.0.1.0";
        public const int timeoutSeconds = 10;
        public const int maxRetries = 20;

        public static readonly string adapterName = NetworkingUtils.SelectNic().Name;

        public static readonly UDPAgent.Config udpConfig = new()
        {
            nodeId = nodeId,
            ip = multicastAddress,
            rxPort = rxPort,
            txPort = txPort,
            timeOut = timeoutSeconds,
            adapterName = adapterName
        };

        public MockClusterSync(NodeType nodeType, bool delayRepeaters = false)
        {
            LocalNode = nodeType switch
            {
                NodeType.Emitter => throw new NotImplementedException("Not yet"),
                NodeType.Repeater => new MockRepeaterNode(this, false, udpConfig),
                _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null)
            };
        }

        public ulong CurrentFrameID { get; set; } = 0;
        public ClusterNode LocalNode { get; }
    }

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
