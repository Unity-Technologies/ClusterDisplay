using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC")]

namespace Unity.ClusterDisplay
{
    #if UNITY_EDITOR
    [AttributeUsage(AttributeTargets.Field)]
    public class ClusterSyncEditorConfigField : Attribute {}
    
    [System.Serializable]
    public struct ClusterSyncEditorConfig
    {
        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceIsMaster")]
        [SerializeField] public bool m_EditorInstanceIsEmitter;
        
        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceMasterCmdLine")]
        [SerializeField] public string m_EditorInstanceEmitterCmdLine;
        [UnityEngine.Serialization.FormerlySerializedAs("m_EditorInstanceSlaveCmdLine")]
        [SerializeField] public string m_EditorInstanceRepeaterCmdLine;

        [SerializeField] public bool m_IgnoreEditorCmdLine;
        public void SetupForEditorTesting(bool isEmitter) => m_EditorInstanceIsEmitter = isEmitter;
        public void UseEditorCmd(bool useEditorCmd) => m_IgnoreEditorCmdLine = !useEditorCmd;

        [SerializeField] public bool m_UseTargetFramerate;
        [SerializeField] public int m_TargetFrameRate;

        public ClusterSyncEditorConfig (bool editorInstanceIsEmitter)
        {
            m_EditorInstanceIsEmitter = editorInstanceIsEmitter;
            m_EditorInstanceEmitterCmdLine = "-emitterNode 0 1 224.0.1.0:25691,25692";
            m_EditorInstanceRepeaterCmdLine = "-node 1 224.0.1.0:25692,25691";

            m_IgnoreEditorCmdLine = false;
            m_UseTargetFramerate = false;
            m_TargetFrameRate = 60;
        }
    }
    #endif
    
    /// <summary>
    /// Need one and only one instance of this class in the scene.
    /// it's responsible for reading the config and applying it, then invoking the
    /// node logic each frame by injecting a call back in the player loop.
    /// 
    /// Note: Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
    /// </summary>
    [DefaultExecutionOrder(Int32.MinValue)]
    [CreateAssetMenu(fileName = "ClusterSync", menuName = "Cluster Display/ClusterSync", order = 1)]
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public class ClusterSync : 
        SingletonScriptableObject<ClusterSync>, 
        IClusterSyncState,
        IClusterDisplayConfigurable
    {
        static ClusterSync() => PreInitialize();

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void PreInitialize()
        {
            ClusterDebug.Log($"Preinitializing: \"{nameof(ClusterSync)}\".");
            ClusterDisplayManager.preInitialize += () =>
            {
                if (TryGetInstance(out var _this))
                    _this.RegisterDelegates();
            };
        }
        
        
        private readonly ClusterDisplayState.IClusterDisplayStateSetter stateSetter = ClusterDisplayState.GetStateStoreSetter();
        private DebugPerf m_FrameRatePerf = new DebugPerf();
        private DebugPerf m_DelayMonitor = new DebugPerf();

        internal delegate void OnClusterDisplayStateChange();
        static internal OnClusterDisplayStateChange onEnableClusterDisplay;
        static internal OnClusterDisplayStateChange onDisableCLusterDisplay;
        
        [HideInInspector]
        bool m_Debugging;
        
        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 CurrentFrameID => m_CurrentFrameID;
        private UInt64 m_CurrentFrameID;
        private bool m_NewFrame;

        #if UNITY_EDITOR
        [HideInInspector][SerializeField] internal ClusterSyncEditorConfig m_ClusterSyncEditorConfig = new ClusterSyncEditorConfig(editorInstanceIsEmitter: true);

        public ClusterSyncEditorConfig EditorConfig
        {
            get => m_ClusterSyncEditorConfig;
            set
            {
                m_ClusterSyncEditorConfig = value;
                EditorUtility.SetDirty(this);
            }
        }
        #endif

        internal ClusterNode m_LocalNode;
        ClusterNode IClusterSyncState.LocalNode => m_LocalNode;
        internal ClusterNode LocalNode => m_LocalNode;

        internal NetworkingStats CurrentNetworkStats => LocalNode.UdpAgent.CurrentNetworkStats;

        /// <summary>
        /// Sends a shutdown request (Useful together with Terminated, to quit the cluster gracefully.)
        /// </summary>
        public void ShutdownAllClusterNodes() => LocalNode?.BroadcastShutdownRequest(); // matters not who triggers it

        /// <summary>
        /// The Local Cluster Node Id.
        /// </summary>
        /// <exception cref="Exception">Throws if the cluster logic is not enabled.</exception>
        public bool TryGetDynamicLocalNodeId (out byte dynamicLocalNodeId)
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
        public string GetDebugString() => LocalNode.GetDebugString() + $"\r\nFPS: { (1 / m_FrameRatePerf.Average):0000}, AvgSynchOvrhead:{m_DelayMonitor.Average*1000:00.0}";

        private void RegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onEnable += EnableClusterDisplay;
            
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onDisable += DisableClusterDisplay;
            
            ClusterDisplayManager.onApplicationQuit -= Quit;
            ClusterDisplayManager.onApplicationQuit += Quit;
        }

        protected override void OnAwake() {}

        private void EnableClusterDisplay()
        {
            #if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            #endif

            /*
            if (LocalNode != null)
                CleanUp();
            */

            stateSetter.SetIsActive(true);
            stateSetter.SetIsTerminated(false);
            stateSetter.SetCLusterLogicEnabled(false);
            NodeState.Debugging = m_Debugging;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = CommandLineParser.targetFPS;

            // Grab command line args related to cluster config
            #if UNITY_EDITOR
            if (!m_ClusterSyncEditorConfig.m_IgnoreEditorCmdLine)
            {
                var editorInstanceCmdLine = m_ClusterSyncEditorConfig.m_EditorInstanceIsEmitter ? 
                    m_ClusterSyncEditorConfig.m_EditorInstanceEmitterCmdLine : 
                    m_ClusterSyncEditorConfig.m_EditorInstanceRepeaterCmdLine;

                CommandLineParser.OverrideArguments(editorInstanceCmdLine.Split(' ').ToList()); 
            }

            if (m_ClusterSyncEditorConfig.m_UseTargetFramerate)
                Application.targetFrameRate = m_ClusterSyncEditorConfig.m_TargetFrameRate;
            #endif
            
            stateSetter.SetCLusterLogicEnabled(CommandLineParser.ClusterLogicSpecified);
            stateSetter.SetIsRepeater(false);

            if (!ClusterDisplayState.IsClusterLogicEnabled)
            {
                ClusterDebug.Log("ClusterRendering is missing command line configuration. Will be dormant.");
                stateSetter.SetIsEmitter(true);
                return;
            }

            if (!TryInitialize())
            {
                stateSetter.SetCLusterLogicEnabled(false);
                return;
            }

            InjectSynchPointInPlayerLoop();
            onEnableClusterDisplay?.Invoke();
           // RPC.RPCExecutor.TrySetup();
        }

        private void DisableClusterDisplay()
        {
            if (!ClusterDisplayState.IsClusterLogicEnabled)
                return;
            CleanUp();
        }

        private void InjectSynchPointInPlayerLoop()
        {
            // Inject into player loop
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            if (initList.Any((x) => x.type == this.GetType()))
                return; // We don't need to assert or insert anything if our loop already exists.

            var indexOfPlayerUpdateTime = initList.FindIndex((x) =>
                
            #if UNITY_2020_2_OR_NEWER
                x.type == typeof(UnityEngine.PlayerLoop.TimeUpdate.WaitForLastPresentationAndUpdateTime));
            #else
                x.type == typeof(UnityEngine.PlayerLoop.Initialization.PlayerUpdateTime));
            #endif
            
            Assert.IsTrue(indexOfPlayerUpdateTime != -1, "Can't find insertion point in player loop for ClusterRendering system");

            initList.Insert(indexOfPlayerUpdateTime + 1, new PlayerLoopSystem()
            {
                type = this.GetType(),
                updateDelegate = SystemUpdate
            });


            newLoop.subSystemList[0].subSystemList = initList.ToArray();

            PlayerLoop.SetPlayerLoop(newLoop);

        }

        private void RemoveSynchPointFromPlayerLoop()
        {
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            var entryToDel = initList.FindIndex((x) => x.type == this.GetType());
            if (entryToDel == -1)
                return; // If the subsystem does not doesn't contain our loop, then we don't need to remove it.
            initList.RemoveAt(entryToDel);

            newLoop.subSystemList[0].subSystemList = initList.ToArray();

            PlayerLoop.SetPlayerLoop(newLoop);
        }

        private bool TryInitializeEmitter()
        {
            // Emitter command line format: -emitterNode nodeId nodeCount ip:rxport,txport
            m_LocalNode =  new EmitterNode(
                this, 
                CommandLineParser.nodeID, 
                CommandLineParser.repeaterCount, 
                CommandLineParser.multicastAddress, 
                CommandLineParser.rxPort, 
                CommandLineParser.txPort, 
                30, 
                CommandLineParser.adapterName);
            
            stateSetter.SetIsEmitter(true);
            stateSetter.SetIsRepeater(false);

            return m_LocalNode.TryStart();
        }

        private bool TryInitializeRepeater()
        {
            // Emitter command line format: -node nodeId ip:rxport,txport
            m_LocalNode = new RepeaterNode(
                this, 
                CommandLineParser.nodeID, 
                CommandLineParser.multicastAddress, 
                CommandLineParser.rxPort, 
                CommandLineParser.txPort, 
                30, 
                CommandLineParser.adapterName);
            
            stateSetter.SetIsEmitter(false);
            stateSetter.SetIsRepeater(true);

            return m_LocalNode.TryStart();
        }
        
        private bool TryInitialize()
        {
            try
            {
                m_Debugging = CommandLineParser.debugFlag;
                
                if (CommandLineParser.TryParseHandshakeTimeout(out var handshakeTimeout))
                    ClusterParams.RegisterTimeout = handshakeTimeout;
                
                if (CommandLineParser.TryParseCommunicationTimeout(out var communicationTimeout))
                    ClusterParams.CommunicationTimeout = communicationTimeout;

                if (CommandLineParser.emitterSpecified)
                {
                    if (!TryInitializeEmitter())
                        return false;
                    return true;
                }

                if (TryInitializeRepeater())
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

        private void SystemUpdate()
        {
            if (Input.GetKeyUp(KeyCode.K) || Input.GetKeyUp(KeyCode.Q))
                Quit();

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
                        m_DelayMonitor.SampleNow();
                        m_DelayMonitor.RefPoint();
                    }
                }
                else
                {
                    m_FrameRatePerf.SampleNow();
                    m_FrameRatePerf.RefPoint();

                    ClusterDebug.Log($"(Frame: {m_CurrentFrameID}): Node is starting frame.");
                    
                    var newFrame = true;
                    m_DelayMonitor.RefPoint();
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
                    
                    ClusterDebug.Log($"(Frame: {m_CurrentFrameID}): Stepping to next frame.");
                    stateSetter.SetFrame(++m_CurrentFrameID);

                    m_DelayMonitor.SampleNow();
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