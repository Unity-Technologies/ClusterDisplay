using System;
using System.Diagnostics;

namespace Unity.ClusterDisplay
{
    internal abstract class ClusterNode
    {
        protected NodeState m_CurrentState;
        protected UDPAgent m_UDPAgent;
        public UDPAgent UdpAgent => m_UDPAgent;

        public byte NodeID => m_UDPAgent.LocalNodeID;
        public UInt64 NodeIDMask => m_UDPAgent.LocalNodeIDMask;

        protected ClusterNode(
            byte nodeID, 
            string ip, 
            int rxPort, 
            int txPort, 
            int timeOut, 
            int maxMTUSize,
            string adapterName)
        {
            if(nodeID >= UDPAgent.MaxSupportedNodeCount)
                throw new ArgumentOutOfRangeException($"Node id must be in the range of [0,{UDPAgent.MaxSupportedNodeCount - 1}]");

            m_UDPAgent = new UDPAgent(
                nodeID, 
                ip, 
                rxPort, 
                txPort, 
                timeOut, 
                maxMTUSize, 
                adapterName);

            Stopwatch.StartNew();
        }

        public virtual bool Start()
        {
            try
            {
                if (!m_UDPAgent.Start())
                {
                    m_CurrentState = new FatalError("Failed to start UDP Agent");
                    m_CurrentState.EnterState(null);
                    return false;
                }

                m_UDPAgent.OnError += OnNetworkingError;

                return true;
            }
            catch (Exception e)
            {
                m_CurrentState = new FatalError( "Failed to start UDP Agent: " + e.Message );
                m_CurrentState.EnterState(null);
                return false;
            }
        }

        public bool DoFrame(bool newFrame)
        {
            m_CurrentState = m_CurrentState?.ProcessFrame(newFrame);

            if (m_CurrentState.GetType() == typeof(Shutdown))
            {
                if (m_UDPAgent.IsTxQueueEmpty)
                {
                    m_UDPAgent.Stop();
                    return false;
                }
            }

            return m_CurrentState.GetType() != typeof(FatalError);
        }

        public void EndFrame () => m_CurrentState?.OnEndFrame();

        public void Exit()
        {
            if(m_CurrentState.GetType() != typeof(Shutdown))
                m_CurrentState = (new Shutdown()).EnterState(m_CurrentState);
            m_UDPAgent.Stop();
        }

        public bool ReadyToProceed => m_CurrentState?.ReadyToProceed ?? true;

        public void BroadcastShutdownRequest()
        {
            var msgHdr = new MessageHeader()
            {
                MessageType = EMessageType.GlobalShutdownRequest,
                Flags = MessageHeader.EFlag.LoopBackToSender | MessageHeader.EFlag.Broadcast,
                OffsetToPayload = 0
            };

            UdpAgent.PublishMessage(msgHdr);
        }

        public virtual string GetDebugString()
        {
            if (ClusterSync.TryGetInstance(out var clusterSync) && clusterSync.TryGetDynamicLocalNodeId(out var dynamicLocalNodeId))
            {
                var stats = clusterSync.CurrentNetworkStats;
                return $"Node {dynamicLocalNodeId} at {clusterSync.CurrentFrameID}\r\n" +
                       $"Network stats: tx[{stats.txQueueSize}], rx[{stats.rxQueueSize}], ack[{stats.pendingAckQueueSize}], rtx[{stats.totalResends}], tot[{stats.msgsSent}], abandoned[{stats.failedMsgs}]\r\n" +
                       $"State: { m_CurrentState.GetDebugString() }";
            }

            return null;
        }

        protected void OnNetworkingError( string message )
        {
            m_CurrentState.PendingStateChange = new FatalError($"Networking error: {message}");
        }
    }

}