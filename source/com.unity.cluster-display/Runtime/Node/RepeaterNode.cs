using System;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay
{
    internal class RepeaterNode : ClusterNode
    {
        public byte EmitterNodeId { get; set; }
        public BitVector EmitterNodeIdMask => BitVector.FromIndex(EmitterNodeId);
        public bool DelayRepeater { get; set; }
        
        
        public override bool HasHardwareSync
        {
            get => m_CurrentState is RepeaterSynchronization {HasHardwareSync: true};
            set
            {
                if (m_CurrentState is RepeaterSynchronization emitter)
                {
                    emitter.HasHardwareSync = value;
                }
            }
        }

        TimeSpan m_HandshakeTimeout;
        TimeSpan m_CommunicationTimeout;

        public struct Config
        {
            public TimeSpan handshakeTimeout;
            public TimeSpan communicationTimeout;

            public bool delayRepeater;
            public UDPAgent.Config config;
        }

        public RepeaterNode(IClusterSyncState clusterSync, Config config)
            : base(clusterSync, config.config)
        {
            DelayRepeater = config.delayRepeater;
            m_HandshakeTimeout = config.handshakeTimeout;
            m_CommunicationTimeout = config.communicationTimeout;
        }

        public override void Start()
        {
            m_CurrentState = new RegisterWithEmitter(clusterSync, new RegisterWithEmitter.Config { communicationTimeout = m_CommunicationTimeout });
            m_CurrentState.EnterState(null);
        }
    }
}