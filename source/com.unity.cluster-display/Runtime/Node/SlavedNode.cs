using System;
using Unity.ClusterDisplay.SlaveStateMachine;

namespace Unity.ClusterDisplay
{
    internal class SlavedNode : ClusterNode
    {
        public byte MasterNodeId { get; set; }
        public UInt64 MasterNodeIdMask => (UInt64) 1 << MasterNodeId;

        public SlavedNode(byte nodeId, string ip, int rxport, int txport, int timeOut, string adapterName)
            : base(nodeId, ip, rxport, txport, timeOut, adapterName )
        {
        }

        public override bool Start()
        {
            if (!base.Start())
                return false;

            m_CurrentState = new RegisterWithMaster(this) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);

            return true;
        }
    }
}