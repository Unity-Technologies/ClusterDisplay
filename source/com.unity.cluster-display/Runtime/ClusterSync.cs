using System;
using System.Linq;
using System.Net;
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

        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 CurrentFrameID => LocalNode.FrameIndex;

        bool m_NewFrame;

        internal ClusterNode LocalNode { get; private set; }

        public NetworkStatistics CurrentNetworkStats => LocalNode.UdpAgent.Stats;

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

            dynamicLocalNodeId = LocalNode.Config.NodeId;
            return true;
        }

        /// <summary>
        /// Debug info.
        /// </summary>
        /// <returns>Returns generic statistics as a string (Average FPS, AvgSyncronization overhead)</returns>
        public string GetDiagnostics()
        {
            if (LocalNode == null)
            {
                return string.Empty;
            }

            var quadroSyncState = GfxPluginQuadroSyncSystem.Instance.FetchState();
            return $"Cluster Sync Instance: {InstanceName},\r\n" +
				   $"Frame Stats:\r\n{LocalNode.GetDebugString(LocalNode.UdpAgent.Stats)}" +
                   $"\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms" +
                   $"\r\n\tAverage Sync Overhead Time: {(m_StartDelayMonitor.Average + m_EndDelayMonitor.Average) * 1000} ms" +
                   $"\r\n\r\n Quadro Sync State:" +
                   $"\r\n\tInitialization: " + quadroSyncState.InitializationState.ToDescriptiveText() +
                   $"\r\n\tSwap group / barrier identifier: {quadroSyncState.SwapGroupId} / {quadroSyncState.SwapBarrierId}" +
                   $"\r\n\tPresent success / failure: {quadroSyncState.PresentedFramesSuccess} / {quadroSyncState.PresentedFramesFailure}";
        }

        void InstanceLog(string msg) => ClusterDebug.Log($"[{nameof(ClusterSync)} instance \"{InstanceName}\"]: {msg}");

        void RegisterDelegates()
        {
            UnRegisterDelegates();

            // Register Do(X) methods with ClusterSyncLooper network fences.
            ClusterSyncLooper.onInstanceDoPreFrame += PreFrame;
            ClusterSyncLooper.onInstanceDoFrame += DoFrame;
            ClusterSyncLooper.onInstancePostFrame += PostFrame;
            ClusterSyncLooper.onInstanceDoLateFrame += DoLateFrame;
        }

        void UnRegisterDelegates()
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

            Application.targetFrameRate = clusterParams.TargetFps;

            IsTerminated = false;
            IsClusterLogicEnabled = clusterParams.ClusterLogicSpecified;
            EmitterIsHeadless = clusterParams.HeadlessEmitter;
            ReplaceHeadlessEmitter = clusterParams.ReplaceHeadlessEmitter && EmitterIsHeadless;

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

#if UNITY_STANDALONE_WIN
            if (NodeRole != NodeRole.Unassigned)
            {
                ClusterSyncWindowsHelpers.CheckNodeSetup();
            }
#endif

            RegisterDelegates();
            ClusterSyncLooper.InjectSynchPointInPlayerLoop();
            onPostEnableClusterDisplay?.Invoke();
        }

        public void DisableClusterDisplay() => CleanUp();

        bool TryInitializeEmitter(ClusterNodeConfig clusterNodeConfig, UDPAgentConfig udpConfig,
            ClusterParams clusterParams)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)} for emitter.");
            try
            {
                var emitterNodeConfig = new EmitterNodeConfig
                {
                    ExpectedRepeaterCount = (byte)clusterParams.RepeaterCount
                };
                udpConfig.ReceivedMessagesType = EmitterNode.ReceiveMessageTypes.ToArray();
                LocalNode = new EmitterNode(clusterNodeConfig, emitterNodeConfig, new UDPAgent(udpConfig));

                NodeRole = NodeRole.Emitter;
                RepeatersDelayedOneFrame = clusterParams.DelayRepeaters;

                return true;
            }
            catch (Exception e)
            {
                ClusterDebug.LogError($"Cannot initialize emitter node: {e.Message}");
                return false;
            }
        }

        bool TryInitializeRepeater(ClusterNodeConfig clusterNodeConfig, UDPAgentConfig udpConfig,
            ClusterParams clusterParams)
        {
            InstanceLog($"Initializing {nameof(ClusterSync)} for repeater.");

            try
            {
                udpConfig.ReceivedMessagesType = RepeaterNode.ReceiveMessageTypes.ToArray();
                LocalNode = new RepeaterNode(clusterNodeConfig, new UDPAgent(udpConfig));

                NodeRole = NodeRole.Repeater;

                return true;
            }
            catch (Exception e)
            {
                ClusterDebug.LogError($"Cannot initialize repeater node: {e.Message}");
                return false;
            }
        }

        bool TryInitialize(ClusterParams clusterParams)
        {
            try
            {
                var nodeConfig = new ClusterNodeConfig
                {
                    NodeId = clusterParams.NodeID,
                    HandshakeTimeout = clusterParams.HandshakeTimeout,
                    CommunicationTimeout = clusterParams.CommunicationTimeout,
                    RepeatersDelayed = clusterParams.DelayRepeaters,
                    EnableHardwareSync = clusterParams.EnableHardwareSync
                };

                var udpAgentConfig = new UDPAgentConfig
                {
                    MulticastIp = IPAddress.Parse(clusterParams.MulticastAddress),
                    Port = clusterParams.Port,
                    AdapterName = clusterParams.AdapterName
                };

                if (clusterParams.EmitterSpecified)
                {
                    if (!TryInitializeEmitter(nodeConfig, udpAgentConfig, clusterParams))
                        return false;
                    return true;
                }

                if (TryInitializeRepeater(nodeConfig, udpAgentConfig, clusterParams))
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
            NodeRole = NodeRole.Unassigned;
            IsClusterLogicEnabled = false;

            LocalNode?.Dispose();
            LocalNode = null;

            ClusterSyncLooper.RemoveSynchPointFromPlayerLoop();

            UnRegisterDelegates();

            onDisableCLusterDisplay?.Invoke();

            InstanceLog($"Flushed.");
        }

        void PreFrame ()
        {
            try
            {
                m_FrameRatePerf.SampleNow();
                m_FrameRatePerf.RefPoint();

                m_StartDelayMonitor.RefPoint();
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

        void DoFrame ()
        {
            try
            {
                LocalNode.DoFrame();
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

        void PostFrame ()
        {
            try
            {
                LocalNode.ConcludeFrame();

                m_StartDelayMonitor.SampleNow();
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

        public void DoLateFrame ()
        {
            try
            {
                m_EndDelayMonitor.RefPoint();
                m_EndDelayMonitor.SampleNow();
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

        void OnException (Exception e)
        {
            IsTerminated = true;

            InstanceLog($"Encountered exception.");
            ClusterDebug.LogException(e);
        }

        void OnFinally ()
        {
            if (IsTerminated)
            {
                CleanUp();
            }
        }
    }
}
