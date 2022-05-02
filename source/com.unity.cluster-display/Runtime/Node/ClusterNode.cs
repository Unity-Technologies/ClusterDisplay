using System;
using System.Diagnostics;

namespace Unity.ClusterDisplay
{
    abstract class ClusterNode
    {
        public UDPAgent UdpAgent => m_UdpAgent;

        public byte NodeID => m_UdpAgent.LocalNodeID;

        public ulong CurrentFrameID { get; private set; }

        /// <summary>
        /// Gets or sets whether there is a layer of synchronization performed
        /// by hardware (e.g. Nvidia Quadro Sync). Default is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// When set to <c>false</c>, all nodes signal when they are ready
        /// to begin a new frame, and the emitter will wait until it receives
        /// the signal from all nodes before allowing the cluster to proceed.
        /// Set this to <c>true</c> if your hardware enforces this at a low level
        /// and it is safe to bypass the wait.
        /// </remarks>
        public virtual bool HasHardwareSync { get; set; }

        protected NodeState m_CurrentState;
        UDPAgent m_UdpAgent;

        protected ClusterNode(UDPAgent.Config config)
        {
            if (config.nodeId >= UDPAgent.MaxSupportedNodeCount)
                throw new ArgumentOutOfRangeException($"Node id must be in the range of [0,{UDPAgent.MaxSupportedNodeCount - 1}]");

            m_UdpAgent = new UDPAgent(config);
            m_UdpAgent.OnError += OnNetworkingError;

            Stopwatch.StartNew();
        }

        public virtual void Start()
        {
            CurrentFrameID = 0;
        }

        public bool DoFrame(bool newFrame)
        {
            m_CurrentState = m_CurrentState?.ProcessFrame(newFrame);

            if (m_CurrentState is Shutdown)
            {
                m_UdpAgent.Stop();
            }

            return m_CurrentState is not FatalError;
        }

        public void DoLateFrame() => m_CurrentState?.ProcessLateFrame();

        public void EndFrame()
        {
            m_CurrentState?.OnEndFrame();
            ++CurrentFrameID;
        }

        public void Exit()
        {
            if (m_CurrentState.GetType() != typeof(Shutdown))
                m_CurrentState = new Shutdown().EnterState(m_CurrentState);
            m_UdpAgent.Stop();
        }

        public bool ReadyToProceed => m_CurrentState?.ReadyToProceed ?? true;
        public bool ReadyForNextFrame => m_CurrentState?.ReadyForNextFrame ?? true;

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

        public virtual string GetDebugString(NetworkingStats networkStats)
        {
            return $"\tNode ID: {NodeID}\r\n\tFrame: {CurrentFrameID}\r\n" +
                $"\tState: {m_CurrentState.GetDebugString()}\r\n" +
                $"\tNetwork stats: \r\n\t\tSend Queue Size: [{networkStats.txQueueSize}], " +
                $"\r\n\t\tReceive Queue Size:[{networkStats.rxQueueSize}], " +
                $"\r\n\t\tACK Queue Size: [{networkStats.pendingAckQueueSize}], " +
                $"\r\n\t\tTotal Resends: [{networkStats.totalResends}], " +
                $"\r\n\t\tMessages Sent: [{networkStats.msgsSent}], " +
                $"\r\n\t\tFailed Messages: [{networkStats.failedMsgs}]";
        }

        protected void OnNetworkingError(string message)
        {
            m_CurrentState.PendingStateChange = new FatalError($"Networking error: {message}");
        }
    }
}
