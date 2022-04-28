using System;
using UnityEngine;

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
        public static ClusterSync Instance => ClusterDisplayManager.ClusterSyncInstance;

        public const string k_DefaultName = "DefaultClusterSync";
        public string InstanceName => m_InstanceName;
        readonly string m_InstanceName;

        public ClusterSync (string instanceName = k_DefaultName)
        {
            m_InstanceName = instanceName;
            RegisterDelegates();

            ClusterDebug.Log($"Created instance of: {nameof(ClusterSync)} with name: \"{instanceName}\".");
        }

        ClusterParams? m_ClusterParams;

        private DebugPerf m_FrameRatePerf = new();
        DebugPerf m_StartDelayMonitor = new();
        DebugPerf m_EndDelayMonitor = new();

        public delegate void OnClusterDisplayStateChange();

        public static OnClusterDisplayStateChange onPreEnableClusterDisplay;
        public static OnClusterDisplayStateChange onPostEnableClusterDisplay;
        public static OnClusterDisplayStateChange onDisableCLusterDisplay;

        bool m_Debugging;

        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 CurrentFrameID => m_CurrentFrameID;

        private UInt64 m_CurrentFrameID;
        private bool m_NewFrame;

        public ClusterNode m_LocalNode;
        ClusterNode IClusterSyncState.LocalNode => m_LocalNode;

        public ClusterNode LocalNode => m_LocalNode;

        public NetworkingStats CurrentNetworkStats => LocalNode.UdpAgent.CurrentNetworkStats;

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

            // Register Do(X) methods with ClusterSyncLooper network fences.
            ClusterSyncLooper.onInstanceDoPreFrame += PreFrame;
            ClusterSyncLooper.onInstanceDoFrame += DoFrame;
            ClusterSyncLooper.onInstancePostFrame += PostFrame;
            ClusterSyncLooper.onInstanceDoLateFrame += DoLateFrame;
        }

        private void UnRegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onApplicationQuit -= Quit;

            ClusterSyncLooper.onInstanceDoPreFrame -= PreFrame;
            ClusterSyncLooper.onInstanceDoFrame -= DoFrame;
            ClusterSyncLooper.onInstancePostFrame -= PostFrame;
            ClusterSyncLooper.onInstanceDoLateFrame -= DoLateFrame;
        }

        public ClusterParams ReadParamsFromCommandLine ()
        {
            m_ClusterParams = new ClusterParams
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
                CommunicationTimeout      = new TimeSpan(0, 0, 0, 0, CommandLineParser.communicationTimeout.Defined ? CommandLineParser.communicationTimeout.Value : 10000)
            };

            return m_ClusterParams.Value;
        }

        public void EnableClusterDisplay()
        {
            if (syncState.IsActive)
                return;

            m_ClusterParams ??= ReadParamsFromCommandLine();
            ClusterParams clusterParams = m_ClusterParams.Value;

            InstanceLog($"Enabling {nameof(ClusterSync)} instance: \"{m_InstanceName}\".");

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = clusterParams.TargetFps;

            syncState.SetIsActive(true);
            syncState.SetIsTerminated(false);
            syncState.SetClusterLogicEnabled(clusterParams.ClusterLogicSpecified);
            syncState.SetNodeID(clusterParams.NodeID);

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

            ClusterSyncLooper.InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        public void DisableClusterDisplay() => CleanUp();

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
                        headlessEmitter     = clusterParams.HeadlessEmitter,
                        repeatersDelayed    = clusterParams.DelayRepeaters,
                        repeaterCount       = clusterParams.RepeaterCount,
                        udpAgentConfig      = config
                    });
            
                syncState.SetIsEmitter(true);
                syncState.SetEmitterIsHeadless(clusterParams.HeadlessEmitter);
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
                    clusterParams.DelayRepeaters,
                    config);

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
                m_Debugging = clusterParams.DebugFlag;
                

                var udpAgentConfig = new UDPAgent.Config
                {
                    nodeId          = clusterParams.NodeID,
                    ip              = clusterParams.MulticastAddress,
                    rxPort          = clusterParams.RXPort,
                    txPort          = clusterParams.TXPort,
                    timeOut         = 30,
                    adapterName     = clusterParams.AdapterName
                };

                if (clusterParams.EmitterSpecified)
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

        public void CleanUp()
        {
            m_ClusterParams = null;

            LocalNode?.Exit();
            m_LocalNode = null;

            ClusterSyncLooper.RemoveSynchPointFromPlayerLoop();

            m_CurrentFrameID = 0;
            syncState.SetIsActive(false);
            syncState.SetClusterLogicEnabled(false);

            UnRegisterDelegates();

            onDisableCLusterDisplay?.Invoke();

            InstanceLog($"Flushed.");
        }

        private void PreFrame ()
        {
            try
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

                    InstanceLog($"(Frame: {m_CurrentFrameID}): Node is starting frame.");

                    m_StartDelayMonitor.RefPoint();
                }

                newFrame = true;
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

        private bool newFrame = false;
        private (bool readyToProceed, bool isTerminated) DoFrame ()
        {
            try
            {
                if (!LocalNode.DoFrame(newFrame))
                {
                    // Game Over!
                    syncState.SetIsTerminated(true);
                }

                newFrame = false;
                return (LocalNode.ReadyToProceed, state.IsTerminated);
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }

            return (true, true);
        }

        private void PostFrame ()
        {
            try
            {
                LocalNode.EndFrame();

                m_StartDelayMonitor.SampleNow();
                InstanceLog(GetDebugString());
                InstanceLog($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");

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

        public bool DoLateFrame ()
        {
            try
            {
                m_EndDelayMonitor.RefPoint();
                LocalNode.DoLateFrame();
                m_EndDelayMonitor.SampleNow();
                return LocalNode.ReadyForNextFrame;
            }

            catch (Exception e)
            {
                OnException(e);
            }

            finally
            {
                OnFinally();
            }

            return true;
        }

        private void OnException (Exception e)
        {
            syncState.SetIsTerminated(true);

            InstanceLog($"Encountered exception.");
            ClusterDebug.LogException(e);
        }

        private void OnFinally ()
        {
            if (state.IsTerminated)
            {
                CleanUp();
            }
        }
    }
}
