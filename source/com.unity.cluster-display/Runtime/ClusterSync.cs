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
    internal partial class ClusterSync
    {
        public static ClusterSync Instance => ClusterDisplayManager.ClusterSyncInstance;

        const string k_DefaultName = "DefaultClusterSync";
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
        public UInt64 CurrentFrameID => LocalNode.CurrentFrameID;

        private bool m_NewFrame;

        internal ClusterNode LocalNode { get; private set; }

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
            if (!StateAccessor.IsClusterLogicEnabled || LocalNode == null)
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
            var quadroSyncState = GfxPluginQuadroSyncSystem.Instance.FetchState();
            return $"Cluster Sync Instance: {m_InstanceName},\r\n" +
				   $"Frame Stats:\r\n{LocalNode.GetDebugString(CurrentNetworkStats)}" +
                   $"\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms" +
                   $"\r\n\tAverage Sync Overhead Time: {(m_StartDelayMonitor.Average + m_EndDelayMonitor.Average) * 1000} ms" +
                   $"\r\n\r\n Quadro Sync State:" +
                   $"\r\n\tInitialization: " + quadroSyncState.InitializationState.ToDescriptiveText() +
                   $"\r\n\tSwap group / barrier identifier: {quadroSyncState.SwapGroupId} / {quadroSyncState.SwapBarrierId}" +
                   $"\r\n\tPresent success / failure: {quadroSyncState.PresentedFramesSuccess} / {quadroSyncState.PresentedFramesFailure}";
        }

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
            if (StateAccessor.IsActive)
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

            m_State.SetIsActive(true);
            m_State.SetIsTerminated(false);
            m_State.SetClusterLogicEnabled(clusterParams.ClusterLogicSpecified);
            m_State.SetNodeID(clusterParams.NodeID);

            onPreEnableClusterDisplay?.Invoke();

            if (!StateAccessor.IsClusterLogicEnabled)
            {
                InstanceLog("ClusterRendering is missing command line configuration. Will be dormant.");
                m_State.SetIsEmitter(true);
                return;
            }

            if (!TryInitialize(clusterParams))
            {
                m_State.SetClusterLogicEnabled(false);
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
                LocalNode = new EmitterNode(
                    new EmitterNode.Config
                    {
                        headlessEmitter     = clusterParams.HeadlessEmitter,
                        repeatersDelayed    = clusterParams.DelayRepeaters,
                        repeaterCount       = clusterParams.RepeaterCount,
                        udpAgentConfig      = config
                    });

                m_State.SetIsEmitter(true);
                m_State.SetEmitterIsHeadless(clusterParams.HeadlessEmitter);
                m_State.SetIsRepeater(false);


                LocalNode.Start();
                return true;
            }
            catch (Exception e)
            {
                ClusterDebug.LogError($"Cannot initialize emitter node: {e.Message}");
                return false;
            }
        }

        private bool TryInitializeRepeater(ClusterParams clusterParams, UDPAgent.Config config)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)}: instance \"{m_InstanceName}\" for repeater.");

            try
            {
                // Emitter command line format: -node nodeId ip:rxport,txport
                LocalNode = new RepeaterNode(config);

                m_State.SetIsEmitter(false);
                m_State.SetIsRepeater(true);

                LocalNode.Start();
                return true;
            }
            catch (Exception e)
            {
                ClusterDebug.LogError($"Cannot initialize repeater node: {e.Message}");
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
            LocalNode = null;

            ClusterSyncLooper.RemoveSynchPointFromPlayerLoop();

            m_State.SetIsActive(false);
            m_State.SetClusterLogicEnabled(false);

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
                        m_State.SetIsTerminated(true);
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

                    InstanceLog($"(Frame: {CurrentFrameID}): Node is starting frame.");

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
                    m_State.SetIsTerminated(true);
                }

                newFrame = false;
                return (LocalNode.ReadyToProceed, m_State.IsTerminated);
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
                InstanceLog($"(Frame: {CurrentFrameID}): Stepping to next frame.");

                m_State.SetFrame(LocalNode.CurrentFrameID);
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
            m_State.SetIsTerminated(true);

            InstanceLog($"Encountered exception.");
            ClusterDebug.LogException(e);
        }

        private void OnFinally ()
        {
            if (StateAccessor.IsTerminated)
            {
                CleanUp();
            }
        }
    }
}
