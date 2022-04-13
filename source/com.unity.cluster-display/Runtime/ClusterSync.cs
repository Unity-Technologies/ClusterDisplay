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
        public static string GetDebugString(string instanceName) => ClusterSync.GetUniqueInstance(instanceName).GetDebugString();
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
        internal interface IState
        {
            public bool IsEmitter { get; }
            public bool EmitterIsHeadless { get; }
            public bool IsRepeater { get; }
            public bool IsClusterLogicEnabled { get; }
            public bool IsActive { get; }
            public bool IsTerminated { get; }
            public ulong Frame { get; }
            public ushort NodeID { get; }
        }

        private class SyncState : IState
        {
            private bool m_IsEmitter;
            private bool m_EmitterIsHeadless;
            private bool m_IsRepeater;
            private bool m_IsClusterLogicEnabled;
            private bool m_IsActive;
            private bool m_IsTerminated;
            private ulong m_Frame;
            private ushort m_NodeID;

            public bool IsEmitter => m_IsEmitter;
            public bool EmitterIsHeadless => !Application.isEditor && m_EmitterIsHeadless;
            public bool IsRepeater => m_IsRepeater;
            public bool IsClusterLogicEnabled => m_IsClusterLogicEnabled;
            public bool IsActive => m_IsActive;
            public bool IsTerminated => m_IsTerminated;
            public ulong Frame => m_Frame;
            public ushort NodeID => m_NodeID;

            public void SetIsActive(bool isActive) => m_IsActive = isActive;
            public void SetCLusterLogicEnabled(bool clusterLogicEnabled) => m_IsClusterLogicEnabled = clusterLogicEnabled;
            public void SetIsEmitter(bool isEmitter) => m_IsEmitter = isEmitter;
            public void SetEmitterIsHeadless(bool headlessEmitter) => m_EmitterIsHeadless = headlessEmitter;
            public void SetIsRepeater(bool isRepeater) => m_IsRepeater = isRepeater;
            public void SetIsTerminated(bool isTerminated) => m_IsTerminated = isTerminated;
            public void SetFrame(ulong frame) => m_Frame = frame;
            public void SetNodeID (ushort nodeId) => m_NodeID = nodeId;
        }

        readonly static Dictionary<string, ClusterSync> k_Instances = new Dictionary<string, ClusterSync>();
        const string k_DefaultName = "DefaultClusterSync";

        public string InstanceName => m_InstanceName;
        readonly string m_InstanceName;

        static string m_InstanceInContext = k_DefaultName;

        internal static void PushInstance(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
                throw new ArgumentNullException(nameof(instanceName));

            if (!k_Instances.ContainsKey(instanceName))
                throw new Exception($"Instance: \"{instanceName}\" does not exist.");

            m_InstanceInContext = instanceName;
        }

        internal static bool InstanceExists (string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
                throw new ArgumentNullException(nameof(instanceName));
            return k_Instances.ContainsKey(instanceName);
        }

        internal static void PopInstance () => m_InstanceInContext = k_DefaultName;

        public static ClusterSync Instance => GetUniqueInstance(m_InstanceInContext);
        public static ClusterSync GetUniqueInstance (string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                throw new ArgumentNullException(nameof(instanceName));
            }

            if (!k_Instances.TryGetValue(instanceName, out var instance))
            {
                return CreateInstance(instanceName);
            }

            return instance;
        }

        private static ClusterSync CreateInstance (string instanceName) =>
            new ClusterSync(instanceName);

        internal static void ClearInstances ()
        {
            foreach (var instance in k_Instances.Values)
            {
                instance.CleanUp();
            }

            k_Instances.Clear();
        }

#if !UNITY_INCLUDE_TESTS
        static ClusterSync() => PreInitialize();

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void PreInitialize()
        {
            ClusterDebug.Log($"Preinitializing: \"{nameof(ClusterSync)}\".");
            ClusterDisplayManager.preInitialize += () =>
                GetUniqueInstance(k_DefaultName).RegisterDelegates();
        }
#endif

        private ClusterSync (string instanceName)
        {
            m_InstanceName = instanceName;
            k_Instances.Add(instanceName, this);

            RegisterDelegates();
        }

        readonly SyncState syncState = new SyncState();
        public IState state => syncState;

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
        public string GetDebugString() => $"Cluster Sync Instance: {m_InstanceName},\r\n" +
			"Frame Stats:\r\n{LocalNode.GetDebugString(CurrentNetworkStats)}" +
            $"\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms" +
            $"\r\n\tAverage Sync Overhead Time: {(m_StartDelayMonitor.Average + m_EndDelayMonitor.Average) * 1000} ms\r\n";

        private void RegisterDelegates()
        {
            UnRegisterDelegates();

            ClusterDisplayManager.onEnable += EnableClusterDisplay;
            ClusterDisplayManager.onDisable += DisableClusterDisplay;
            ClusterDisplayManager.onApplicationQuit += Quit;
        }

        private void UnRegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onApplicationQuit -= Quit;
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
                m_HandshakeTimeout          = new TimeSpan(0, 0, 0, 0, CommandLineParser.handshakeTimeout.Value),
                m_CommunicationTimeout      = new TimeSpan(0, 0, 0, 0, CommandLineParser.communicationTimeout.Value)
            };
        }

        private void EnableClusterDisplay ()
        {
            if (m_ClusterParams != null)
                PrePopulateClusterParams();

            EnableClusterDisplay(m_ClusterParams.Value);
        }

        private void EnableClusterDisplay(ClusterParams clusterParams)
        {
            ClusterDebug.Log($"Enabling {nameof(ClusterSync)} instance: \"{m_InstanceName}\".");

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = clusterParams.m_TargetFps;

            syncState.SetIsActive(true);
            syncState.SetIsTerminated(false);
            syncState.SetCLusterLogicEnabled(false);
            syncState.SetNodeID(clusterParams.m_NodeID);

            onPreEnableClusterDisplay?.Invoke();

            syncState.SetCLusterLogicEnabled(clusterParams.m_ClusterLogicSpecified);
            syncState.SetIsRepeater(false);

            if (!ClusterDisplayState.IsClusterLogicEnabled)
            {
                ClusterDebug.Log("ClusterRendering is missing command line configuration. Will be dormant.");
                syncState.SetIsEmitter(true);
                return;
            }

            if (!TryInitialize(clusterParams))
            {
                syncState.SetCLusterLogicEnabled(false);
                return;
            }

            InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        private void DisableClusterDisplay() => CleanUp();

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
            ClusterDebug.Log($"Initializing {nameof(ClusterSync)}: instance \"{m_InstanceName}\" for emitter.");
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
            ClusterDebug.Log($"Initializing {nameof(ClusterSync)}: instance \"{m_InstanceName}\" for repeater.");

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

        private void CleanUp()
        {
            LocalNode?.Exit();
            m_LocalNode = null;

            RemoveSynchPointFromPlayerLoop();

            m_CurrentFrameID = 0;
            syncState.SetIsActive(false);
            syncState.SetCLusterLogicEnabled(false);

            UnRegisterDelegates();

            onDisableCLusterDisplay?.Invoke();
            k_Instances.Remove(m_InstanceName);
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

        private void PreFrame ()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.kKey.isPressed || Keyboard.current.qKey.isPressed)
                Quit();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                Quit();
#endif

            if (m_Debugging)
            {
                if (m_NewFrame)
                    m_FrameRatePerf.SampleNow();

                if (!LocalNode.DoFrame(m_NewFrame))
                {
                    // Game Over!
                    syncState.SetIsTerminated(true);
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

                m_StartDelayMonitor.RefPoint();
            }

            newFrame = true;
        }

        private bool newFrame = false;

        private void OnException (Exception e)
        {
            syncState.SetIsTerminated(true);
            ClusterDebug.LogException(e);
        }

        private void OnFinally ()
        {
            if (ClusterDisplayState.IsTerminated)
                CleanUp();
        }

        private void DoFrame (out bool readyToProceed, out bool isTerminated)
        {
            readyToProceed = LocalNode.ReadyToProceed;
            isTerminated = ClusterDisplayState.IsTerminated;

            try
            {
                if (!LocalNode.DoFrame(newFrame))
                {
                    // Game Over!
                    syncState.SetIsTerminated(true);
                }

                readyToProceed = LocalNode.ReadyToProceed;
                isTerminated = ClusterDisplayState.IsTerminated;
                newFrame = false;
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }

        }

        private void PostFrame ()
        {
            try
            {
                LocalNode.EndFrame();

                m_StartDelayMonitor.SampleNow();
                ClusterDebug.Log(GetDebugString());
                ClusterDebug.Log($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");

                syncState.SetFrame(++m_CurrentFrameID);
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }
        }

        private static void SystemUpdate()
        {
            var instances = k_Instances.Values.ToArray();
            bool allReadyToProceed, allIsTerminated;

            foreach (var instance in instances)
            {
                instance.PreFrame();
            }

            do
            {
                allReadyToProceed = true;
                allIsTerminated = false;

                foreach (var instance in instances)
                {
                    if (instance.m_Debugging)
                        continue;

                    instance.DoFrame(out var readyToProceed, out var isTermindated);

                    allReadyToProceed &= readyToProceed;
                    allIsTerminated &= isTermindated;
                }

            } while (allReadyToProceed && !allIsTerminated);

            foreach (var instance in instances)
            {
                instance.PostFrame();
            }
        }
    }
}
