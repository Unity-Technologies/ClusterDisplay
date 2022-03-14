﻿using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
using System.Runtime.CompilerServices;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Editor")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Graphics")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.Graphics.Example")]
[assembly: InternalsVisibleTo("Unity.ClusterDisplay.RPC")]

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
    internal class ClusterSync : 
        IClusterSyncState
    {
        static ClusterSync m_Instance;
        public static ClusterSync Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new ClusterSync();
                }

                return m_Instance;
            }
        }
        static ClusterSync() => PreInitialize();

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void PreInitialize()
        {
            ClusterDebug.Log($"Preinitializing: \"{nameof(ClusterSync)}\".");
            ClusterDisplayManager.preInitialize += () => Instance.RegisterDelegates();
        }
        
        
        private readonly ClusterDisplayState.IClusterDisplayStateSetter stateSetter = ClusterDisplayState.GetStateStoreSetter();
        private DebugPerf m_FrameRatePerf = new DebugPerf();
        private DebugPerf m_DelayMonitor = new DebugPerf();

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
        public string GetDebugString() => $"Frame Stats:\r\n{LocalNode.GetDebugString()}\r\n\r\n\tAverage Frame Time: {(m_FrameRatePerf.Average * 1000)} ms\r\n\tAverage Sync Overhead Time: {m_DelayMonitor.Average * 1000} ms\r\n";

        private void RegisterDelegates()
        {
            ClusterDisplayManager.onEnable -= EnableClusterDisplay;
            ClusterDisplayManager.onEnable += EnableClusterDisplay;
            
            ClusterDisplayManager.onDisable -= DisableClusterDisplay;
            ClusterDisplayManager.onDisable += DisableClusterDisplay;
            
            ClusterDisplayManager.onApplicationQuit -= Quit;
            ClusterDisplayManager.onApplicationQuit += Quit;
        }

        private void EnableClusterDisplay()
        {
            #if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            #endif

            NodeState.Debugging = m_Debugging;

            Application.targetFrameRate = CommandLineParser.targetFPS;

            stateSetter.SetIsActive(true);
            stateSetter.SetIsTerminated(false);
            stateSetter.SetCLusterLogicEnabled(false);

            onPreEnableClusterDisplay?.Invoke();

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
            onPostEnableClusterDisplay?.Invoke();
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

        private bool TryInitializeEmitter(UDPAgent.Config config)
        {
            try
            {
                // Emitter command line format: -emitterNode nodeId nodeCount ip:rxport,txport
                m_LocalNode = new EmitterNode(
                    this,
                    new EmitterNode.Config
                    {
                        headlessEmitter = CommandLineParser.HeadlessEmitter,
                        repeatersDelayed = CommandLineParser.delayRepeaters,
                        repeaterCount = CommandLineParser.repeaterCount,
                        udpAgentConfig = config
                    });
            
                stateSetter.SetIsEmitter(true);
                stateSetter.SetEmitterIsHeadless(CommandLineParser.HeadlessEmitter);
                stateSetter.SetIsRepeater(false);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot initialize emitter node: {e.Message}");
                return false;
            }
        }

        private bool TryInitializeRepeater(UDPAgent.Config config)
        {
            try
            {
                // Emitter command line format: -node nodeId ip:rxport,txport
                m_LocalNode = new RepeaterNode(
                    this, 
                    CommandLineParser.delayRepeaters, 
                    config);
            
                stateSetter.SetIsEmitter(false);
                stateSetter.SetIsRepeater(true);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot initialize repeater node: {e.Message}");
                return false;
            }
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

                var udpAgentConfig = new UDPAgent.Config
                {
                    nodeId = CommandLineParser.nodeID,
                    ip = CommandLineParser.multicastAddress,
                    rxPort = CommandLineParser.rxPort,
                    txPort = CommandLineParser.txPort,
                    timeOut = 30,
                    adapterName = CommandLineParser.adapterName
                };
                
                if (CommandLineParser.emitterSpecified)
                {
                    if (!TryInitializeEmitter(udpAgentConfig))
                        return false;
                    return true;
                }

                if (TryInitializeRepeater(udpAgentConfig))
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
                    
                    m_DelayMonitor.SampleNow();
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