using System;

namespace Unity.ClusterRendering
{
    internal abstract class BaseNode
    {
        protected BaseState m_CurrentState;
        protected UDPAgent m_UDPAgent;
        public UInt64 OutSequenceId { get; set; }
        public UDPAgent UdpAgent => m_UDPAgent;

        public byte NodeID => m_UDPAgent.LocalNodeID;
        public UInt64 NodeIDMask => m_UDPAgent.LocalNodeIDMask;

        protected BaseNode(byte nodeID, string ip, int rxPort, int txPort, int timeOut)
        {
            if(nodeID >= UDPAgent.MaxSupportedNodeCount)
                throw new ArgumentOutOfRangeException($"Node id must be in the range of [0,{UDPAgent.MaxSupportedNodeCount - 1}]");
            m_UDPAgent = new UDPAgent(nodeID, ip, rxPort, txPort, timeOut);
        }

        public virtual bool Start()
        {
            try
            {
                if (!m_UDPAgent.Start())
                {
                    m_CurrentState = new FatalError() { Message = "Failed to start UDP Agent" };
                    m_CurrentState.EnterState(null);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                m_CurrentState = new FatalError() { Message = "Failed to start UDP Agent: " + e.Message };
                m_CurrentState.EnterState(null);
                return false;
            }
        }

        public bool DoFrame(bool frameAdvance)
        {
            m_CurrentState = m_CurrentState?.ProcessFrame(frameAdvance);
            return m_CurrentState.GetType() != typeof(FatalError) && m_CurrentState.GetType() != typeof(Shutdown);
        }

        public void Exit()
        {
            if(m_CurrentState.GetType() != typeof(Shutdown))
                m_CurrentState = (new Shutdown()).EnterState(m_CurrentState);
        }

        public bool ReadyToProceed => m_CurrentState?.ReadyToProceed ?? true;
        
    }

}