using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.ClusterDisplay.Utils;
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

    /// <summary>
    /// The input subsystem synchronized by the cluster.
    /// </summary>
    public enum InputSync
    {
        /// <summary>
        /// Don't synchronize inputs.
        /// </summary>
        None = 0,
#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Synchronize inputs from the Input System package (new).
        /// </summary>
        InputSystem = 1,
#endif
        /// <summary>
        /// Synchronize inputs from legacy Input Manager (old).
        /// </summary>
        Legacy = 2
    }

    [Serializable]
    public struct ClusterParams
    {
        public bool ClusterLogicSpecified;
        public bool EmitterSpecified;

        public byte NodeID;
        public int RepeaterCount;

        public int Port;

        /// <remarks>
        /// Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
        /// </remarks>
        public string MulticastAddress;
        public string AdapterName;

        public int TargetFps;
        public bool DelayRepeaters;
        public bool HeadlessEmitter;
        public bool ReplaceHeadlessEmitter;

        public InputSync InputSync;

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
                // Note that the default state of Cluster Display is "off" in a standalone player.
                // It is automatically enabled when the required arguments are given in the command
                // line, or enabled through external logic. See
                ClusterLogicSpecified = false,
                Port = 25690,
                MulticastAddress = "224.0.1.0",
                HandshakeTimeout = TimeSpan.FromMilliseconds(10000),
                CommunicationTimeout = TimeSpan.FromMilliseconds(5000),
                Fence = FrameSyncFence.Hardware,
                TargetFps = -1,
            };
    }

    /// <summary>
    /// Indicates that this type contains a method for processing cluster display parameters.
    /// </summary>
    /// <seealso cref="ClusterParamProcessorMethodAttribute"/>
    /// <remarks>
    /// TODO: We can do this better with a static interface, which will be available
    /// in C# 11.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class ClusterParamProcessorAttribute : Attribute { }

    /// <summary>
    /// Marks that this method will be automatically invoked on Cluster Display initialization
    /// during the processing of parameters.
    /// </summary>
    /// <remarks>
    /// This method must be <see langword="static"/>, take one parameter of type <see cref="ClusterParams"/> and
    /// return a <see cref="ClusterParams"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClusterParamProcessorMethodAttribute : Attribute { }

    [ClusterParamProcessor]
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
        [ClusterParamProcessorMethod, UnityEngine.Scripting.Preserve]
        public static ClusterParams ApplyCommandLine(ClusterParams clusterParams)
        {
            if (CommandLineParser.clusterDisplayLogicSpecified)
            {
                clusterParams.ClusterLogicSpecified = true;
                clusterParams.EmitterSpecified = CommandLineParser.emitterSpecified.Value;
                if (clusterParams.EmitterSpecified)
                {
                    ApplyArgument(ref clusterParams.RepeaterCount, CommandLineParser.repeaterCount);
                }
            }

            // In the player, enable cluster logic if we detected an emitter or repeater argument
#if !UNITY_EDITOR
            clusterParams.ClusterLogicSpecified |= CommandLineParser.clusterDisplayLogicSpecified;
#endif

            ApplyArgument(ref clusterParams.NodeID, CommandLineParser.nodeID);
            ApplyArgument(ref clusterParams.MulticastAddress, CommandLineParser.multicastAddress);
            ApplyArgument(ref clusterParams.Port, CommandLineParser.port);
            ApplyArgument(ref clusterParams.DelayRepeaters, CommandLineParser.delayRepeaters);
            ApplyArgument(ref clusterParams.ReplaceHeadlessEmitter, CommandLineParser.replaceHeadlessEmitter);
            ApplyArgument(ref clusterParams.HeadlessEmitter, CommandLineParser.headlessEmitter);
            ApplyArgument(ref clusterParams.AdapterName, CommandLineParser.adapterName);
            ApplyArgument(ref clusterParams.TargetFps, CommandLineParser.targetFps);

            if (CommandLineParser.handshakeTimeout.Defined)
            {
                clusterParams.HandshakeTimeout = TimeSpan.FromMilliseconds(CommandLineParser.handshakeTimeout.Value);
            }

            if (CommandLineParser.communicationTimeout.Defined)
            {
                clusterParams.CommunicationTimeout = TimeSpan.FromMilliseconds(CommandLineParser.communicationTimeout.Value);
            }

            if (CommandLineParser.disableQuadroSync.Defined)
            {
                clusterParams.Fence = FrameSyncFence.Network;
            }

            return clusterParams;
        }

        public static ClusterParams PreProcess(this ClusterParams clusterParams)
        {
            const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

            bool HasCorrectSignature(MethodInfo methodInfo)
            {
                return methodInfo.GetParameters()
                        .Select(pi => pi.ParameterType)
                        .SequenceEqual(new[] { typeof(ClusterParams) }) &&
                    methodInfo.ReturnType == typeof(ClusterParams);
            }

            foreach (var (processorType, attribute) in AttributeUtility.GetAllTypes<ClusterParamProcessorAttribute>())
            {
                var processorMethods = processorType.GetMethods(bindingFlags)
                    .Where(m => m.IsDefined(typeof(ClusterParamProcessorMethodAttribute)));

                foreach (var method in processorMethods)
                {
                    if (!HasCorrectSignature(method))
                    {
                        ClusterDebug.Log(
                            $"Found cluster parameter processor {method.Name} with incorrect signature. Ignoring");
                        continue;
                    }

                    clusterParams = (ClusterParams)method.Invoke(null, new object[] { clusterParams });
                }
            }

            return clusterParams;
        }
    }
}
