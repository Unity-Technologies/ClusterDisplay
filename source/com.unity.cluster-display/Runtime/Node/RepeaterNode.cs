using System;
using Unity.ClusterDisplay.RepeaterStateMachine;

namespace Unity.ClusterDisplay
{
    internal class RepeaterNode : ClusterNode
    {
        public byte EmitterNodeId { get; set; }
        public UInt64 EmitterNodeIdMask => (UInt64) 1 << EmitterNodeId;

        public RepeaterNode(byte nodeId, string ip, int rxport, int txport, int timeOut, int maxMTUSize, string adapterName)
            : base(nodeId, ip, rxport, txport, timeOut, maxMTUSize, adapterName )
        {
        }

        public override bool Start()
        {
            if (!base.Start())
                return false;

            m_CurrentState = new RegisterWithEmitter(this) {MaxTimeOut = ClusterParams.RegisterTimeout};
            m_CurrentState.EnterState(null);

            return true;
        }
    }
}