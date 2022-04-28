using System;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;

namespace Unity.ClusterDisplay
{
    class RepeaterNode : ClusterNode
    {
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

        public RepeaterNode(UDPAgent.Config config)
            : base(config)
        {
            m_CurrentState = HardwareSyncInitState.Create(this, true);
        }

        public override void Start()
        {
            base.Start();
            m_CurrentState.EnterState(null);
        }
    }
}
