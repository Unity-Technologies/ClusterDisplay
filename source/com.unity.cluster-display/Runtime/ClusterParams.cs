using System;

namespace Unity.ClusterDisplay
{
    internal struct ClusterParams
    {
        public bool         m_DebugFlag;

        public bool         m_ClusterLogicSpecified;
        public bool         m_EmitterSpecified;

        public byte         m_NodeID;
        public int          m_RepeaterCount;

        public int          m_RXPort; 
        public int          m_TXPort;

        public string       m_MulticastAddress;
        public string       m_AdapterName;

        public int          m_TargetFps;
        public bool         m_DelayRepeaters;
        public bool         m_HeadlessEmitter;

        public TimeSpan     m_HandshakeTimeout;
        public TimeSpan     m_CommunicationTimeout;
    }
}