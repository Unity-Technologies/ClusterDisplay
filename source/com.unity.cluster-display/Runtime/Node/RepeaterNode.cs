using System;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay
{
    class RepeaterNode : ClusterNode
    {
        public struct Config
        {
            public bool EnableHardwareSync;
            public UDPAgent.Config UdpAgentConfig;
        }

        public byte EmitterNodeId { get; set; }
        public BitVector EmitterNodeIdMask => BitVector.FromIndex(EmitterNodeId);

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

        public RepeaterNode(Config config)
            : base(config.UdpAgentConfig)
        {
            m_CurrentState = config.EnableHardwareSync
                ? HardwareSyncInitState.Create(this)
                : new RegisterWithEmitter(this);
        }

        public override void Start()
        {
            base.Start();
            m_CurrentState.EnterState(null);
        }
    }
}
