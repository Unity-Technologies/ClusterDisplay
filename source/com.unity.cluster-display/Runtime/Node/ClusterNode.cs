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

        protected internal IClusterSyncState clusterSync;

        protected ClusterNode(
            IClusterSyncState clusterSync,
            UDPAgent.Config config)
        {
            if (config.nodeId >= UDPAgent.MaxSupportedNodeCount)
                throw new ArgumentOutOfRangeException($"Node id must be in the range of [0,{UDPAgent.MaxSupportedNodeCount - 1}]");

            m_UDPAgent = new UDPAgent(config);
            m_UDPAgent.OnError += OnNetworkingError;

            this.clusterSync = clusterSync;
            Stopwatch.StartNew();
        }

        public abstract void Start();

        public bool DoFrame(bool newFrame)
        {
            m_CurrentState = m_CurrentState?.ProcessFrame(newFrame);

            if (m_CurrentState.GetType() == typeof(Shutdown))
            {
                m_UDPAgent.Stop();
            }

            return m_CurrentState.GetType() != typeof(FatalError);
        }

        public void EndFrame() => m_CurrentState?.OnEndFrame();

        public void Exit()
        {
            if (m_CurrentState.GetType() != typeof(Shutdown))
                m_CurrentState = (new Shutdown(clusterSync)).EnterState(m_CurrentState);
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
            if (ClusterSync.Instance.TryGetDynamicLocalNodeId(out var dynamicLocalNodeId))
            {
                var stats = ClusterSync.Instance.CurrentNetworkStats;
                return $"\tNode ID: {dynamicLocalNodeId}\r\n\tFrame: {clusterSync.CurrentFrameID}\r\n" +
                    $"\tState: {m_CurrentState.GetDebugString()}\r\n" +
                    $"\tNetwork stats: \r\n\t\tSend Queue Size: [{stats.txQueueSize}], " +
                    $"\r\n\t\tReceive Queue Size:[{stats.rxQueueSize}], " +
                    $"\r\n\t\tACK Queue Size: [{stats.pendingAckQueueSize}], " +
                    $"\r\n\t\tTotal Resends: [{stats.totalResends}], " +
                    $"\r\n\t\tMessages Sent: [{stats.msgsSent}], " +
                    $"\r\n\t\tFailed Messages: [{stats.failedMsgs}]";
            }

            return null;
        }

        protected void OnNetworkingError(string message)
        {
            m_CurrentState.PendingStateChange = new FatalError(clusterSync, $"Networking error: {message}");
        }
    }
}
