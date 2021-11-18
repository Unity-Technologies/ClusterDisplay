using System;
using Unity.ClusterDisplay.RepeaterStateMachine;

namespace Unity.ClusterDisplay
{
    internal class RepeaterNode : ClusterNode
    {
        public byte EmitterNodeId { get; set; }
        public UInt64 EmitterNodeIdMask => (UInt64) 1 << EmitterNodeId;

        public RepeaterNode(IClusterSyncState clusterSync, byte nodeId, string ip, int rxport, int txport, int timeOut, string adapterName)
            : base(clusterSync, nodeId, ip, rxport, txport, timeOut, adapterName )
        {
        }

        public override bool TryStart()
        {
            if (!base.TryStart())
                return false;

            m_CurrentState = new RegisterWithEmitter(clusterSync, this) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);

            return true;
        }
    }
}