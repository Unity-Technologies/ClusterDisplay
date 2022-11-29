using System;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Method used to implement the fence at each synch point (the beginning of each frame).
    /// </summary>
    /// <remarks>
    /// This fence makes no guarantees that all frames are delivered simultaneously by all the nodes. This can be
    /// mitigated in some circumstances by using a fence that also implements a swap barrier (i.e. Nvidia Quadro Sync),
    /// which removes the "tearing" effect when nodes render at different rates.
    /// </remarks>
    public enum FrameSyncFence
    {
        /// <summary>
        /// Use the network protocol: emitter waits for all repeaters to report that they are finished
        /// rendering the previous frame before it signals the beginning of a new frame.
        /// </summary>
        Network,
        /// <summary>
        /// Use the available hardware (platform-dependent).
        /// </summary>
        /// <remarks>
        /// Currently supports the following hardware implementations:
        /// <ul>
        /// <li> Nvidia Quadro Sync II (swap barrier)</li>
        /// </ul>
        /// If hardware cannot be initialized, will fall back to <see cref="Network"/>.
        /// </remarks>
        Hardware,
        /// <summary>
        /// The fence is implemented by a third party. No fence is inserted by Cluster Display.
        /// </summary>
        External
    }

    public struct ClusterParams
    {
        public bool           ClusterLogicSpecified;
        public bool           EmitterSpecified;

        public byte           NodeID;
        public int            RepeaterCount;

        public int            Port;

        public string         MulticastAddress;
        public string         AdapterName;

        public int            TargetFps;
        public bool           DelayRepeaters;
        public bool           HeadlessEmitter;
        public bool           ReplaceHeadlessEmitter;

        public TimeSpan       HandshakeTimeout;
        public TimeSpan       CommunicationTimeout;
        public FrameSyncFence Fence;

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
                Fence                     = CommandLineParser.disableQuadroSync.Defined ? FrameSyncFence.Network : FrameSyncFence.Hardware
            };
    }
}
