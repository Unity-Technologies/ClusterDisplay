using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static Unity.ClusterDisplay.Utils.PlayerLoopExtensions;
using System.Collections.Generic;

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
        public static string GetDebugString(string instanceName) => ClusterSync.GetInstance(instanceName).GetDebugString();
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
    internal class ClusterSync :
        IClusterSyncState
    {
        readonly static Dictionary<string, ClusterSync> k_Instances = new Dictionary<string, ClusterSync>();
        const string k_DefaultName = "DefaultClusterSync";

        public string InstanceName => m_InstanceName;
        private readonly string m_InstanceName;

        public static ClusterSync Instance => GetInstance(k_DefaultName);
        public static ClusterSync GetInstance (string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
                throw new ArgumentNullException(nameof(instanceName));

            if (!k_Instances.TryGetValue(instanceName, out var instance))
            {
                return CreateInstance(k_DefaultName);
            }

            return instance;
        }

        private static ClusterSync CreateInstance (string instanceName)
        {
            var instance = new ClusterSync(instanceName);
            k_Instances.Add(instanceName, instance);
            return instance;
        }

#if !UNITY_INCLUDE_TESTS
        static ClusterSync() => PreInitialize();

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreInitialize() => PreInitialize(k_DefaultName);

        internal static void PreInitialize(string instanceName)
        {
            ClusterDebug.Log($"Preinitializing: \"{nameof(ClusterSync)}\".");
            ClusterDisplayManager.preInitialize += () => GetInstance(instanceName).RegisterDelegates();
        }
#endif

        private ClusterSync (string instanceName)
        {
            m_InstanceName = instanceName;
            RegisterDelegates();
        }
        private readonly ClusterDisplayState.IClusterDisplayStateSetter stateSetter = ClusterDisplayState.GetStateStoreSetter();
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
            if (!ClusterDisplayState.IsClusterLogicEnabled || LocalNode == null)
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
        public string GetDebugString()
        {
            return $"Cluster Sync Instance: {m_InstanceName},\r\nFrame Stats:\r\n{LocalNode.GetDebugString(CurrentNetworkStats)}\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms\r\n\tAverage Sync Overhead Time: {m_DelayMonitor.Average * 1000} ms\r\n";
        }

        private void RegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onEnable += EnableClusterDisplay;

            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onDisable += DisableClusterDisplay;

            ClusterDisplayManager.onApplicationQuit -= Quit;
            ClusterDisplayManager.onApplicationQuit += Quit;
        }

        private void EnableClusterDisplay ()
        {
            CommandLineParser.TryParseHandshakeTimeout(out var handshakeTimeout);
            CommandLineParser.TryParseCommunicationTimeout(out var communicationTimeout);

            var clusterParams = new ClusterParams
            {
                m_DebugFlag                 = CommandLineParser.debugFlag,
                m_ClusterLogicSpecified     = CommandLineParser.ClusterLogicSpecified,
                m_EmitterSpecified          = CommandLineParser.emitterSpecified,
                m_NodeID                    = CommandLineParser.nodeID,
                m_RXPort                    = CommandLineParser.rxPort,
                m_TXPort                    = CommandLineParser.txPort,
                m_MulticastAddress          = CommandLineParser.multicastAddress,
                m_AdapterName               = CommandLineParser.adapterName,
                m_TargetFps                 = CommandLineParser.targetFPS,
                m_DelayRepeaters            = CommandLineParser.delayRepeaters,
                m_HandshakeTimeout          = handshakeTimeout,
                m_CommunicationTimeout      = communicationTimeout
            };

            EnableClusterDisplay(clusterParams);
        }

        private void EnableClusterDisplay(ClusterParams clusterParams)
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = clusterParams.m_TargetFps;

            stateSetter.SetIsActive(true);
            stateSetter.SetIsTerminated(false);
            stateSetter.SetCLusterLogicEnabled(false);

            onPreEnableClusterDisplay?.Invoke();

            stateSetter.SetCLusterLogicEnabled(clusterParams.m_ClusterLogicSpecified);
            stateSetter.SetIsRepeater(false);

            if (!ClusterDisplayState.IsClusterLogicEnabled)
            {
                ClusterDebug.Log("ClusterRendering is missing command line configuration. Will be dormant.");
                stateSetter.SetIsEmitter(true);
                return;
            }

            if (!TryInitialize(clusterParams))
            {
                stateSetter.SetCLusterLogicEnabled(false);
                return;
            }

            InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        private void DisableClusterDisplay()
        {
            if (!ClusterDisplayState.IsClusterLogicEnabled)
                return;
            CleanUp();
        }

        struct ClusterDisplayStartFrame { }

        struct ClusterDisplayLateUpdate { }

        void InjectSynchPointInPlayerLoop()
        {
            RegisterUpdate<TimeUpdate.WaitForLastPresentationAndUpdateTime, ClusterDisplayStartFrame>(
                SystemUpdate);
            RegisterUpdate<PostLateUpdate, ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        void RemoveSynchPointFromPlayerLoop()
        {
            DeregisterUpdate<ClusterDisplayStartFrame>(SystemUpdate);
            DeregisterUpdate<ClusterDisplayLateUpdate>(SystemLateUpdate);
        }

        private bool TryInitializeEmitter(ClusterParams clusterParams, UDPAgent.Config config)
        {
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
            
                stateSetter.SetIsEmitter(true);
                stateSetter.SetEmitterIsHeadless(clusterParams.m_HeadlessEmitter);
                stateSetter.SetIsRepeater(false);


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
            try
            {
                // Emitter command line format: -node nodeId ip:rxport,txport
                m_LocalNode = new RepeaterNode(
                    this, 
                    new RepeaterNode.Config
                    {
                        handshakeTimeout = clusterParams.m_HandshakeTimeout,
                        delayRepeater = clusterParams.m_DelayRepeaters,
                        config = config,
                    });

                stateSetter.SetIsEmitter(false);
                stateSetter.SetIsRepeater(true);

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

        private void CleanUp()
        {
            LocalNode?.Exit();
            m_LocalNode = null;

            RemoveSynchPointFromPlayerLoop();

            m_CurrentFrameID = 0;
            stateSetter.SetIsActive(false);
            stateSetter.SetCLusterLogicEnabled(false);

            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;

            onDisableCLusterDisplay?.Invoke();
        }

        void SystemLateUpdate()
        {
            m_EndDelayMonitor.RefPoint();
            while (LocalNode is {ReadyForNextFrame: false} node)
            {
                node.DoLateFrame();
            }

            m_EndDelayMonitor.SampleNow();
        }

        private void SystemUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.kKey.isPressed || Keyboard.current.qKey.isPressed)
                Quit();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                Quit();
#endif

            try
            {
                if (m_Debugging)
                {
                    if (m_NewFrame)
                        m_FrameRatePerf.SampleNow();

                    if (!LocalNode.DoFrame(m_NewFrame))
                    {
                        // Game Over!
                        stateSetter.SetIsTerminated(true);
                    }

                    m_NewFrame = LocalNode.ReadyToProceed;
                    if (m_NewFrame)
                    {
                        m_StartDelayMonitor.SampleNow();
                        m_StartDelayMonitor.RefPoint();
                    }
                }
                else
                {
                    m_FrameRatePerf.SampleNow();
                    m_FrameRatePerf.RefPoint();

                    ClusterDebug.Log($"(Frame: {m_CurrentFrameID}): Node is starting frame.");

                    var newFrame = true;
                    m_StartDelayMonitor.RefPoint();
                    do
                    {
                        if (!LocalNode.DoFrame(newFrame))
                        {
                            // Game Over!
                            stateSetter.SetIsTerminated(true);
                        }

                        newFrame = false;
                    } while (!LocalNode.ReadyToProceed && !ClusterDisplayState.IsTerminated);

                    LocalNode.EndFrame();

                    m_StartDelayMonitor.SampleNow();
                    ClusterDebug.Log(GetDebugString());
                    ClusterDebug.Log($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");

                    stateSetter.SetFrame(++m_CurrentFrameID);
                }
            }

            catch (Exception e)
            {
                stateSetter.SetIsTerminated(true);
                ClusterDebug.LogException(e);
            }

            finally
            {
                if (ClusterDisplayState.IsTerminated)
                    CleanUp();
            }
        }
    }
}
