using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static Unity.ClusterDisplay.Utils.PlayerLoopExtensions;
using System.Collections.Generic;
using System.Linq;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    public static class ClusterSyncDebug
    {
        public static string GetDebugString () => ClusterSync.Instance.GetDebugString();
    }

    /// <summary>
    /// Need one and only one instance of this class in the scene.
    /// it's responsible for reading the config and applying it, then invoking the
    /// node logic each frame by injecting a call back in the player loop.
    /// 
    /// Note: Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal partial class ClusterSync : IClusterSyncState
    {
        internal static ClusterSync Instance => ClusterDisplayManager.ClusterSyncInstance;

        internal const string k_DefaultName = "DefaultClusterSync";
        public string InstanceName => m_InstanceName;
        readonly string m_InstanceName;

        internal ClusterSync (string instanceName = k_DefaultName)
        {
            m_InstanceName = instanceName;
            RegisterDelegates();

            ClusterDebug.Log($"Created instance of: {nameof(ClusterSync)} with name: \"{instanceName}\".");
        }

        ClusterParams ? m_ClusterParams = null;

        private DebugPerf m_FrameRatePerf = new();
        DebugPerf m_StartDelayMonitor = new();
        DebugPerf m_EndDelayMonitor = new();

        internal delegate void OnClusterDisplayStateChange();

        static internal OnClusterDisplayStateChange onPreEnableClusterDisplay;
        static internal OnClusterDisplayStateChange onPostEnableClusterDisplay;
        static internal OnClusterDisplayStateChange onDisableCLusterDisplay;

        [HideInInspector]
        bool m_Debugging;

        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 CurrentFrameID => m_CurrentFrameID;

        private UInt64 m_CurrentFrameID;
        private bool m_NewFrame;

        internal ClusterNode m_LocalNode;
        ClusterNode IClusterSyncState.LocalNode => m_LocalNode;

        internal ClusterNode LocalNode => m_LocalNode;

        internal NetworkingStats CurrentNetworkStats => LocalNode.UdpAgent.CurrentNetworkStats;

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
        public bool HasHardwareSync
        {
            get => LocalNode.HasHardwareSync;
            set => LocalNode.HasHardwareSync = value;
        }

        /// <summary>
        /// Sends a shutdown request (Useful together with Terminated, to quit the cluster gracefully.)
        /// </summary>
        public void ShutdownAllClusterNodes() => LocalNode?.BroadcastShutdownRequest(); // matters not who triggers it

        /// <summary>
        /// The Local Cluster Node Id.
        /// </summary>
        /// <exception cref="Exception">Throws if the cluster logic is not enabled.</exception>
        public bool TryGetDynamicLocalNodeId(out byte dynamicLocalNodeId)
        {
            if (!state.IsClusterLogicEnabled || LocalNode == null)
            {
                dynamicLocalNodeId = 0;
                return false;
            }

            dynamicLocalNodeId = LocalNode.NodeID;
            return true;
        }

        /// <summary>
        /// Debug info.
        /// </summary>
        /// <returns>Returns generic statistics as a string (Average FPS, AvgSyncronization overhead)</returns>
        public string GetDebugString() => $"Cluster Sync Instance: {m_InstanceName},\r\n" +
			"Frame Stats:\r\n{LocalNode.GetDebugString(CurrentNetworkStats)}" +
            $"\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms" +
            $"\r\n\tAverage Sync Overhead Time: {(m_StartDelayMonitor.Average + m_EndDelayMonitor.Average) * 1000} ms\r\n";

        private void InstanceLog(string msg) => ClusterDebug.Log($"[{nameof(ClusterSync)} instance \"{m_InstanceName}\"]: {msg}");

        private void RegisterDelegates()
        {
            UnRegisterDelegates();

            ClusterDisplayManager.onEnable += EnableClusterDisplay;
            ClusterDisplayManager.onDisable += DisableClusterDisplay;
            ClusterDisplayManager.onApplicationQuit += Quit;

            onInstanceDoPreFrame += PreFrame;
            onInstanceDoFrame += DoFrame;
            onInstancePostFrame += PostFrame;
            onInstanceDoLateFrame += DoLateFrame;
        }

        private void UnRegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onApplicationQuit -= Quit;

            onInstanceDoPreFrame -= PreFrame;
            onInstanceDoFrame -= DoFrame;
            onInstancePostFrame -= PostFrame;
            onInstanceDoLateFrame -= DoLateFrame;
        }

        internal void PrePopulateClusterParams ()
        {
            m_ClusterParams = new ClusterParams
            {
                m_DebugFlag                 = CommandLineParser.debugFlag.Value,
                m_ClusterLogicSpecified     = CommandLineParser.clusterDisplayLogicSpecified,
                m_EmitterSpecified          = CommandLineParser.emitterSpecified.Value,
                m_NodeID                    = CommandLineParser.nodeID.Value,
                m_RepeaterCount             = CommandLineParser.emitterSpecified.Value ? CommandLineParser.repeaterCount.Value : 0,
                m_RXPort                    = CommandLineParser.rxPort.Value,
                m_TXPort                    = CommandLineParser.txPort.Value,
                m_MulticastAddress          = CommandLineParser.multicastAddress.Value,
                m_AdapterName               = CommandLineParser.adapterName.Value,
                m_TargetFps                 = CommandLineParser.targetFps.Value,
                m_DelayRepeaters            = CommandLineParser.delayRepeaters.Value,
                m_HeadlessEmitter           = CommandLineParser.headlessEmitter.Value,
                m_HandshakeTimeout          = new TimeSpan(0, 0, 0, 0, CommandLineParser.handshakeTimeout.Defined ? CommandLineParser.handshakeTimeout.Value : 10000),
                m_CommunicationTimeout      = new TimeSpan(0, 0, 0, 0, CommandLineParser.communicationTimeout.Defined ? CommandLineParser.communicationTimeout.Value : 10000)
            };
        }

        internal void EnableClusterDisplay ()
        {
            if (syncState.IsActive)
                return;

            if (m_ClusterParams == null)
                PrePopulateClusterParams();

            EnableClusterDisplay(m_ClusterParams.Value);
        }

        private void EnableClusterDisplay(ClusterParams clusterParams)
        {
            InstanceLog($"Enabling {nameof(ClusterSync)} instance: \"{m_InstanceName}\".");

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = clusterParams.m_TargetFps;

            syncState.SetIsActive(true);
            syncState.SetIsTerminated(false);
            syncState.SetClusterLogicEnabled(clusterParams.m_ClusterLogicSpecified);
            syncState.SetNodeID(clusterParams.m_NodeID);

            onPreEnableClusterDisplay?.Invoke();

            if (!state.IsClusterLogicEnabled)
            {
                InstanceLog("ClusterRendering is missing command line configuration. Will be dormant.");
                syncState.SetIsEmitter(true);
                return;
            }

            if (!TryInitialize(clusterParams))
            {
                syncState.SetClusterLogicEnabled(false);
                return;
            }

            InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        internal void DisableClusterDisplay()
        {
            if (!state.IsClusterLogicEnabled)
                return;
            CleanUp();
        }

        struct ClusterDisplayStartFrame { }

        struct ClusterDisplayLateUpdate { }

        static void InjectSynchPointInPlayerLoop()
        {
            RegisterUpdate<TimeUpdate.WaitForLastPresentationAndUpdateTime, ClusterDisplayStartFrame>(
                SystemUpdate);
            RegisterUpdate<PostLateUpdate, ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        static void RemoveSynchPointFromPlayerLoop()
        {
            DeregisterUpdate<ClusterDisplayStartFrame>(SystemUpdate);
            DeregisterUpdate<ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        private bool TryInitializeEmitter(ClusterParams clusterParams, UDPAgent.Config config)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)}: instance \"{m_InstanceName}\" for emitter.");
            try
            {
                // Emitter command line format: -emitterNode nodeId nodeCount ip:rxport,txport
                m_LocalNode = new EmitterNode(
                    this,
                    new EmitterNode.Config
                    {
                        headlessEmitter     = clusterParams.m_HeadlessEmitter,
                        repeatersDelayed    = clusterParams.m_DelayRepeaters,
                        repeaterCount       = clusterParams.m_RepeaterCount,
                        handshakeTimeout    = clusterParams.m_HandshakeTimeout,
                        udpAgentConfig      = config
                    });
            
                syncState.SetIsEmitter(true);
                syncState.SetEmitterIsHeadless(clusterParams.m_HeadlessEmitter);
                syncState.SetIsRepeater(false);


                LocalNode.Start();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot initialize emitter node: {e.Message}");
                return false;
            }
        }

        private bool TryInitializeRepeater(ClusterParams clusterParams, UDPAgent.Config config)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)}: instance \"{m_InstanceName}\" for repeater.");

            try
            {
                // Emitter command line format: -node nodeId ip:rxport,txport
                m_LocalNode = new RepeaterNode(
                    this, 
                    new RepeaterNode.Config
                    {
                        handshakeTimeout = clusterParams.m_HandshakeTimeout,
                        delayRepeater = clusterParams.m_DelayRepeaters,
                        udpAgentConfig = config,
                    });

                syncState.SetIsEmitter(false);
                syncState.SetIsRepeater(true);

                LocalNode.Start();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot initialize repeater node: {e.Message}");
                return false;
            }
        }

        private bool TryInitialize(ClusterParams clusterParams)
        {
            try
            {
                m_Debugging = clusterParams.m_DebugFlag;

                var udpAgentConfig = new UDPAgent.Config
                {
                    nodeId          = clusterParams.m_NodeID,
                    ip              = clusterParams.m_MulticastAddress,
                    rxPort          = clusterParams.m_RXPort,
                    txPort          = clusterParams.m_TXPort,
                    timeOut         = 30,
                    adapterName     = clusterParams.m_AdapterName
                };
                
                if (clusterParams.m_EmitterSpecified)
                {
                    if (!TryInitializeEmitter(clusterParams, udpAgentConfig))
                        return false;
                    return true;
                }

                if (TryInitializeRepeater(clusterParams, udpAgentConfig))
                    return true;

                throw new Exception("Cluster command arguments requires a \"-emitterNode\" or \"-node\" flag.");
            }

            catch (Exception e)
            {
                ClusterDebug.LogError("Invalid command line arguments for configuring ClusterRendering.");
                ClusterDebug.LogException(e);

                CleanUp();
                return false;
            }
        }

        private void Quit() =>
            ShutdownAllClusterNodes();

        internal void CleanUp()
        {
            LocalNode?.Exit();
            m_LocalNode = null;

            RemoveSynchPointFromPlayerLoop();

            m_CurrentFrameID = 0;
            syncState.SetIsActive(false);
            syncState.SetClusterLogicEnabled(false);

            UnRegisterDelegates();

            onDisableCLusterDisplay?.Invoke();

            InstanceLog($"Flushed.");
        }
    }
}
