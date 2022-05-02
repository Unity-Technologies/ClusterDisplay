using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Need one and only one instance of this class in the scene.
    /// it's responsible for reading the config and applying it, then invoking the
    /// node logic each frame by injecting a call back in the player loop.
    ///
    /// Note: Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
    /// </summary>
    partial class ClusterSync
    {
        const string k_DefaultName = "DefaultClusterSync";
        public string InstanceName { get; }

        public ClusterSync (string instanceName = k_DefaultName)
        {
            InstanceName = instanceName;

            ClusterDebug.Log($"Created instance of: {nameof(ClusterSync)} with name: \"{instanceName}\".");
        }

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
        /// Sends a shutdown request (Useful together with Terminated, to quit the cluster gracefully.)
        /// </summary>
        public void ShutdownAllClusterNodes() => LocalNode?.BroadcastShutdownRequest(); // matters not who triggers it

        /// <summary>
        /// Get the Node ID if cluster logic is enabled.
        /// </summary>
        /// <returns><see langword="true"/> if the cluster logic is enabled.</returns>
        public bool TryGetDynamicLocalNodeId(out byte dynamicLocalNodeId)
        {
            if (!IsClusterLogicEnabled)
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
        public string GetDiagnostics()
        {
            var quadroSyncState = GfxPluginQuadroSyncSystem.Instance.FetchState();
            return $"Cluster Sync Instance: {InstanceName},\r\n" +
				   $"Frame Stats:\r\n{LocalNode.GetDebugString(CurrentNetworkStats)}" +
                   $"\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms" +
                   $"\r\n\tAverage Sync Overhead Time: {(m_StartDelayMonitor.Average + m_EndDelayMonitor.Average) * 1000} ms" +
                   $"\r\n\r\n Quadro Sync State:" +
                   $"\r\n\tInitialization: " + quadroSyncState.InitializationState.ToDescriptiveText() +
                   $"\r\n\tSwap group / barrier identifier: {quadroSyncState.SwapGroupId} / {quadroSyncState.SwapBarrierId}" +
                   $"\r\n\tPresent success / failure: {quadroSyncState.PresentedFramesSuccess} / {quadroSyncState.PresentedFramesFailure}";
        }

        private void InstanceLog(string msg) => ClusterDebug.Log($"[{nameof(ClusterSync)} instance \"{InstanceName}\"]: {msg}");

        private void RegisterDelegates()
        {
            UnRegisterDelegates();

            // Register Do(X) methods with ClusterSyncLooper network fences.
            ClusterSyncLooper.onInstanceDoPreFrame += PreFrame;
            ClusterSyncLooper.onInstanceDoFrame += DoFrame;
            ClusterSyncLooper.onInstancePostFrame += PostFrame;
            ClusterSyncLooper.onInstanceDoLateFrame += DoLateFrame;
        }

        private void UnRegisterDelegates()
        {
            ClusterSyncLooper.onInstanceDoPreFrame -= PreFrame;
            ClusterSyncLooper.onInstanceDoFrame -= DoFrame;
            ClusterSyncLooper.onInstancePostFrame -= PostFrame;
            ClusterSyncLooper.onInstanceDoLateFrame -= DoLateFrame;
        }

        public void EnableClusterDisplay(ClusterParams clusterParams)
        {
            if (IsClusterLogicEnabled)
                return;

            InstanceLog($"Enabling {nameof(ClusterSync)}.");

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = clusterParams.TargetFps;

            IsTerminated = false;
            IsClusterLogicEnabled = clusterParams.ClusterLogicSpecified;

            onPreEnableClusterDisplay?.Invoke();

            if (!IsClusterLogicEnabled)
            {
                InstanceLog("ClusterRendering is missing command line configuration. Will be dormant.");
                NodeRole = NodeRole.Unassigned;
                return;
            }

            if (!TryInitialize(clusterParams))
            {
                IsClusterLogicEnabled = false;
                return;
            }

            RegisterDelegates();
            ClusterSyncLooper.InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        public void DisableClusterDisplay() => CleanUp();

        private bool TryInitializeEmitter(ClusterParams clusterParams, UDPAgent.Config config)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)} for emitter.");
            try
            {
                // Emitter command line format: -emitterNode nodeId nodeCount ip:rxport,txport
                LocalNode = new EmitterNode(
                    new EmitterNode.Config
                    {
                        headlessEmitter     = clusterParams.HeadlessEmitter,
                        repeatersDelayed    = clusterParams.DelayRepeaters,
                        repeaterCount       = clusterParams.RepeaterCount,
						enableHardwareSync  = clusterParams.EnableHardwareSync,
                        MainConfig =
                        {
                            HandshakeTimeout = clusterParams.HandshakeTimeout,
                            CommunicationTimeout = clusterParams.CommunicationTimeout,
                            UdpAgentConfig = config
                        }
                    });

                NodeRole = NodeRole.Emitter;
                EmitterIsHeadless = clusterParams.HeadlessEmitter;

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
            InstanceLog($"Initializing {nameof(ClusterSync)} for repeater.");

            try
            {
                LocalNode = new RepeaterNode(new RepeaterNode.Config
                {
                    EnableHardwareSync = clusterParams.EnableHardwareSync,
                    MainConfig =
                    {
                        UdpAgentConfig = config,
                        HandshakeTimeout = clusterParams.HandshakeTimeout,
                        CommunicationTimeout = clusterParams.CommunicationTimeout
                    }
                });

                NodeRole = NodeRole.Repeater;

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

        public void CleanUp()
        {
            LocalNode?.Exit();
            LocalNode = null;

            ClusterSyncLooper.RemoveSynchPointFromPlayerLoop();

            IsClusterLogicEnabled = false;

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
                    ShutdownAllClusterNodes();
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                    ShutdownAllClusterNodes();
#endif

                if (m_Debugging)
                {
                    if (m_NewFrame)
                        m_FrameRatePerf.SampleNow();

                    if (!LocalNode.DoFrame(m_NewFrame))
                    {
                        // Game Over!
                        IsTerminated = true;
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
                    IsTerminated = true;
                }

                newFrame = false;
                return (LocalNode.ReadyToProceed, IsTerminated);
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
                InstanceLog(GetDiagnostics());
                InstanceLog($"(Frame: {CurrentFrameID}): Stepping to next frame.");
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
            IsTerminated = true;

            InstanceLog($"Encountered exception.");
            ClusterDebug.LogException(e);
        }

        private void OnFinally ()
        {
            if (IsTerminated)
            {
                CleanUp();
            }
        }
    }
}
