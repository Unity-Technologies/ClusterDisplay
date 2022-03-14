using System;
using Unity.ClusterDisplay.RepeaterStateMachine;

namespace Unity.ClusterDisplay
{
    internal class RepeaterNode : ClusterNode
    {
        public byte EmitterNodeId { get; set; }
        public UInt64 EmitterNodeIdMask => (UInt64) 1 << EmitterNodeId;
        public bool DelayRepeater { get; set; }

        public RepeaterNode(IClusterSyncState clusterSync, bool delayRepeater, UDPAgent.Config config)
            : base(clusterSync, config)
        {
            m_CurrentState = new RegisterWithEmitter(clusterSync) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);
            DelayRepeater = delayRepeater;
        }
    }
}