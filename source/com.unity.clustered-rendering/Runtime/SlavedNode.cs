using System;
using Unity.ClusterRendering.SlaveStateMachine;

namespace Unity.ClusterRendering
{
    internal class SlavedNode : BaseNode
    {
#if UNITY_EDITOR
        private bool m_RunNetworkLogicOnly;
#endif

        public byte MasterNodeId { get; set; }
        public UInt64 MasterNodeIdMask => (UInt64) 1 << MasterNodeId;

        public SlavedNode(byte nodeId, string ip, int rxport, int txport, int timeOut, bool runNetworkLogicOnly) : base(nodeId, ip, rxport, txport, timeOut )
        {
#if UNITY_EDITOR
            m_RunNetworkLogicOnly = runNetworkLogicOnly;
#endif
        }

        public override bool Start()
        {
            if (!base.Start())
                return false;

            m_CurrentState = new RegisterWithMaster(this);
            m_CurrentState.EnterState(null);

            return true;
        }
    }
}