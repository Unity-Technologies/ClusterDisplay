using System;
using System.Runtime.CompilerServices;
using UnityEngine;

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

    [Serializable]
    public struct ClusterParams
    {
        public bool           ClusterLogicSpecified;
        public bool           EmitterSpecified;

        public byte           NodeID;
        public int            RepeaterCount;

        public int            Port;

        /// <remarks>
        /// Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
        /// </remarks>
        public string         MulticastAddress;
        public string         AdapterName;

        public int            TargetFps;
        public bool           DelayRepeaters;
        public bool           HeadlessEmitter;
        public bool           ReplaceHeadlessEmitter;

        public TimeSpan HandshakeTimeout
        {
            get => TimeSpan.FromSeconds(m_HandshakeTimeoutSec);
            set => m_HandshakeTimeoutSec = (float)value.TotalSeconds;
        }

        public TimeSpan CommunicationTimeout
        {
            get => TimeSpan.FromSeconds(m_CommTimeoutSec);
            set => m_CommTimeoutSec = (float)value.TotalSeconds;
        }

        public FrameSyncFence Fence;

        [SerializeField]
        float m_HandshakeTimeoutSec;

        [SerializeField]
        float m_CommTimeoutSec;

        public static ClusterParams Default { get; } =
            new()
            {
                ClusterLogicSpecified = true,
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                HandshakeTimeout = TimeSpan.FromMilliseconds(10000),
                CommunicationTimeout = TimeSpan.FromMilliseconds(5000),
                Fence = FrameSyncFence.Hardware
            };

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

    static class ClusterParamExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ApplyArgument<T>(ref T target, CommandLineParser.BaseArgument<T> arg)
        {
            if (arg.Defined)
            {
                target = arg.Value;
            }
        }

        /// <summary>
        /// Override the given parameters with values given in the command line (if present)
        /// </summary>
        /// <param name="clusterParams"></param>
        /// <returns>The new set of cluster parameters.</returns>
        /// <remarks>
        /// When running in the player, if no cluster arguments are given in the command line,
        /// cluster display will be disabled.
        /// </remarks>
        public static ClusterParams ApplyCommandLine(this ClusterParams clusterParams)
        {
            var newParams = clusterParams;

            if (CommandLineParser.clusterDisplayLogicSpecified)
            {
                newParams.ClusterLogicSpecified = true;
                newParams.EmitterSpecified = CommandLineParser.emitterSpecified.Value;
                if (newParams.EmitterSpecified)
                {
                    ApplyArgument(ref newParams.RepeaterCount, CommandLineParser.repeaterCount);
                }
            }
            // In the player, we disable all cluster logic if no cluster arguments are given.
            #if !UNITY_EDITOR
            newParams.ClusterLogicSpecified = CommandLineParser.clusterDisplayLogicSpecified;
            #endif

            ApplyArgument(ref newParams.NodeID, CommandLineParser.nodeID);
            ApplyArgument(ref newParams.MulticastAddress, CommandLineParser.multicastAddress);
            ApplyArgument(ref newParams.Port, CommandLineParser.port);
            ApplyArgument(ref newParams.DelayRepeaters, CommandLineParser.delayRepeaters);
            ApplyArgument(ref newParams.ReplaceHeadlessEmitter, CommandLineParser.replaceHeadlessEmitter);
            ApplyArgument(ref newParams.HeadlessEmitter, CommandLineParser.headlessEmitter);

            if (CommandLineParser.handshakeTimeout.Defined)
            {
                newParams.HandshakeTimeout = TimeSpan.FromMilliseconds(CommandLineParser.handshakeTimeout.Value);
            }

            if (CommandLineParser.communicationTimeout.Defined)
            {
                newParams.CommunicationTimeout = TimeSpan.FromMilliseconds(CommandLineParser.communicationTimeout.Value);
            }

            if (CommandLineParser.disableQuadroSync.Defined)
            {
                newParams.Fence = FrameSyncFence.Network;
            }

            return newParams;
        }
    }
}
