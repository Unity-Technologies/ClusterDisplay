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
        public bool         EnableHardwareSync;

        public static ClusterParams FromCommandLine() =>
            new()
            {
                DebugFlag                 = CommandLineParser.debugFlag.Value,
                ClusterLogicSpecified     = CommandLineParser.clusterDisplayLogicSpecified,
                EmitterSpecified          = CommandLineParser.emitterSpecified.Value,
                NodeID                    = CommandLineParser.nodeID.Value,
                RepeaterCount             = CommandLineParser.emitterSpecified.Value ? CommandLineParser.repeaterCount.Value : 0,
                RXPort                    = CommandLineParser.rxPort.Value,
                TXPort                    = CommandLineParser.txPort.Value,
                MulticastAddress          = CommandLineParser.multicastAddress.Value,
                AdapterName               = CommandLineParser.adapterName.Value,
                TargetFps                 = CommandLineParser.targetFps.Value,
                DelayRepeaters            = CommandLineParser.delayRepeaters.Value,
                HeadlessEmitter           = CommandLineParser.headlessEmitter.Value,
                HandshakeTimeout          = new TimeSpan(0, 0, 0, 0, CommandLineParser.handshakeTimeout.Defined ? CommandLineParser.handshakeTimeout.Value : 10000),
                CommunicationTimeout      = new TimeSpan(0, 0, 0, 0, CommandLineParser.communicationTimeout.Defined ? CommandLineParser.communicationTimeout.Value : 10000),
                EnableHardwareSync        = !CommandLineParser.disableQuadroSync.Defined
            };
    }
}
