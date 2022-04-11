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

        public RepeaterNode(IClusterSyncState clusterSync, bool delayRepeater, UDPAgent.Config config)
            : base(clusterSync, config)
        {
            DelayRepeater = delayRepeater;
        }

        public override void Start()
        {
            m_CurrentState = HardwareSyncInitState.Create(clusterSync);
            m_CurrentState.EnterState(null);
        }
    }
}
