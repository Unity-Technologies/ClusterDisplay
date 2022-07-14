using System;

namespace Unity.ClusterDisplay
{
    struct ClusterParams
    {
        public bool         ClusterLogicSpecified;
        public bool         EmitterSpecified;

        public byte         NodeID;
        public int          RepeaterCount;

        public int          Port;

        public string       MulticastAddress;
        public string       AdapterName;

        public int          TargetFps;
        public bool         DelayRepeaters;
        public bool         HeadlessEmitter;
        public bool         ReplaceHeadlessEmitter;

        public TimeSpan     HandshakeTimeout;
        public TimeSpan     CommunicationTimeout;
        public bool         EnableHardwareSync;

        public static ClusterParams FromCommandLine() =>
            new()
            {
                ClusterLogicSpecified     = CommandLineParser.clusterDisplayLogicSpecified,
                EmitterSpecified          = CommandLineParser.emitterSpecified.Value,
                NodeID                    = CommandLineParser.nodeID.Value,
                RepeaterCount             = CommandLineParser.emitterSpecified.Value ? CommandLineParser.repeaterCount.Value : 0,
                Port                      = CommandLineParser.port.Value,
                MulticastAddress          = CommandLineParser.multicastAddress.Value,
                AdapterName               = CommandLineParser.adapterName.Value,
                TargetFps                 = CommandLineParser.targetFps.Value,
                DelayRepeaters            = CommandLineParser.delayRepeaters.Defined,
                HeadlessEmitter           = CommandLineParser.headlessEmitter.Defined,
                ReplaceHeadlessEmitter    = CommandLineParser.replaceHeadlessEmitter.Defined,
                HandshakeTimeout          = TimeSpan.FromMilliseconds(CommandLineParser.handshakeTimeout.Defined ? CommandLineParser.handshakeTimeout.Value : 10000),
                CommunicationTimeout      = TimeSpan.FromMilliseconds(CommandLineParser.communicationTimeout.Defined ? CommandLineParser.communicationTimeout.Value : 10000),
                EnableHardwareSync        = !CommandLineParser.disableQuadroSync.Defined
            };
    }
}
