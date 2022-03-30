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

        public RepeaterNode(IClusterSyncState clusterSync, bool delayRepeater, UDPAgent.Config config)
            : base(clusterSync, config)
        {
            DelayRepeater = delayRepeater;
        }

        public override void Start()
        {
            m_CurrentState = new RegisterWithEmitter(clusterSync) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);
        }
    }
}