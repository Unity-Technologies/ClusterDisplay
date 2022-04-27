using System;

namespace Unity.ClusterDisplay
{
    struct ClusterParams
    {
        public bool         debugFlag;

        public bool         clusterLogicSpecified;
        public bool         emitterSpecified;

        public byte         nodeID;
        public int          repeaterCount;

        public int          rxPort; 
        public int          txPort;

        public string       multicastAddress;
        public string       adapterName;

        public int          targetFps;
        public bool         delayRepeaters;
        public bool         headlessEmitter;

        public TimeSpan     handshakeTimeout;
        public TimeSpan     communicationTimeout;
    }
}
