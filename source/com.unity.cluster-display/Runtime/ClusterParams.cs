using System;

namespace Unity.ClusterDisplay
{
    struct ClusterParams
    {
        public bool         DebugFlag;

        public bool         ClusterLogicSpecified;
        public bool         EmitterSpecified;

        public byte         NodeID;
        public int          RepeaterCount;

        public int          RXPort; 
        public int          TXPort;

        public string       MulticastAddress;
        public string       AdapterName;

        public int          TargetFps;
        public bool         DelayRepeaters;
        public bool         HeadlessEmitter;

        public TimeSpan     HandshakeTimeout;
        public TimeSpan     CommunicationTimeout;
    }
}
