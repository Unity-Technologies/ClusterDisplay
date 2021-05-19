using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
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
    public partial class ClusterSync : SingletonMonoBehaviour<ClusterSync>
    {

        [HideInInspector]
        bool m_Debugging;
        private bool m_NewFrame = true;

        private readonly ClusterDisplayState.IClusterDisplayStateSetter stateSetter = ClusterDisplayState.GetStateStoreSetter();


        private DebugPerf m_FrameRatePerf = new DebugPerf();

        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 FrameCount => LocalNode.CurrentFrameID;

        private DebugPerf m_DelayMonitor = new DebugPerf();

        // public static bool Active => TryGetInstance(out var instance) && instance.m_ClusterLogicEnabled;


        [SerializeField] private ClusterDisplayResources _clusterDisplayResources;
        public ClusterDisplayResources Resources => _clusterDisplayResources;

#if UNITY_EDITOR
        public string m_EditorCmdLine = "";
#endif
        internal ClusterNode LocalNode { get; set; }

        internal NetworkingStats CurrentNetworkStats => LocalNode.UdpAgent.CurrentNetworkStats;

        /// <summary>
        /// Sends a shutdown request (Useful together with Terminated, to quit the cluster gracefully.)
        /// </summary>
        public void ShutdownAllClusterNodes()
        {
            LocalNode.BroadcastShutdownRequest(); // matters not who triggers it
        }


        internal byte ConfiguredLocalNodeId
        {
            get
            {
                if(ClusterDisplayState.IsClusterLogicEnabled)
                    return LocalNode.NodeID;
                else
                {
                    throw new Exception("Cluster Rendering not active. No local node present");
                }
            }
        }

        /// <summary>
        /// The Local Cluster Node Id.
        /// </summary>
        /// <exception cref="Exception">Throws if the cluster logic is not enabled.</exception>
        public byte DynamicLocalNodeId => ConfiguredLocalNodeId;

        /// <summary>
        /// Debug info.
        /// </summary>
        /// <returns>Returns generic statistics as a string (Average FPS, AvgSyncronization overhead)</returns>
        public string GetDebugString()
        {
            return LocalNode.GetDebugString() + $"\r\nFPS: { (1 / m_FrameRatePerf.Average):0000}, AvgSynchOvrhead:{m_DelayMonitor.Average*1000:00.0}";
        }

#if UNITY_EDITOR
        private void GetResources ()
        {
            if (Resources != null)
                return;

            // Search all assets by our desired type.
            var assets = AssetDatabase.FindAssets($"t:{nameof(ClusterDisplayResources)}");
            if (assets.Length == 0)
                throw new Exception($"No valid instances of: {nameof(ClusterDisplayResources)} exist in the project.");

            _clusterDisplayResources = AssetDatabase.LoadAssetAtPath<ClusterDisplayResources>(AssetDatabase.GUIDToAssetPath(assets[0]));
            Debug.Log($"Applied instance of: {nameof(ClusterDisplayResources)} named: \"{_clusterDisplayResources.name}\" to cluster display settings.");

            EditorUtility.SetDirty(this);
        }

        private void OnValidate() => GetResources();
#endif

        private void OnEnable()
        {
            stateSetter.SetIsTerminated(false);
            stateSetter.SetCLusterLogicEnabled(false);
            NodeState.Debugging = m_Debugging;

            // Grab command line args related to cluster config
            var args = System.Environment.GetCommandLineArgs().ToList();
#if UNITY_EDITOR
            args = m_EditorCmdLine.Split(' ').ToList();
#endif
            var startIndex = args.FindIndex((x) => x == "-masterNode" || x == "-node");
            stateSetter.SetCLusterLogicEnabled(startIndex > -1);

            if (!ClusterDisplayState.IsClusterLogicEnabled)
            {
                Debug.Log("ClusterRendering is missing command line configuration. Will be dormant.");
                return;
            }

            if (!ProcessCommandLine(args))
                stateSetter.SetCLusterLogicEnabled(false);

            if(ClusterDisplayState.IsClusterLogicEnabled)
            {
                InjectSynchPointInPlayerLoop();

                if (ClusterDisplayState.IsSlave)
                    RPCExecutor.TrySetup();
            }
        }

        private void OnDisable()
        {
            if (!ClusterDisplayState.IsClusterLogicEnabled)
                return;

            LocalNode.Exit();
            RemoveSynchPointFromPlayerLoop();

            if (ClusterDisplayState.IsSlave)
                RPCExecutor.RemovePlayerLoops();
        }

        void Update()
        {
            if (!ClusterDisplayState.IsClusterLogicEnabled)
                return;

            if (ClusterDisplayState.IsTerminated)
            {
                this.enabled = false;
                stateSetter.SetCLusterLogicEnabled(false);
                return;
            }
        }

        private void InjectSynchPointInPlayerLoop()
        {
            // Inject into player loop
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            var indexOfPlayerUpdateTime = initList.FindIndex((x) =>
#if UNITY_2020_2_OR_NEWER
                x.type == typeof(UnityEngine.PlayerLoop.TimeUpdate.WaitForLastPresentationAndUpdateTime));
#else
                x.type == typeof(UnityEngine.PlayerLoop.Initialization.PlayerUpdateTime));
#endif
            Assert.IsFalse(initList.Any((x) => x.type == this.GetType()), "Player loop already has a ClusterRendering system entry registered.");
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

            initList.RemoveAt(entryToDel);

            newLoop.subSystemList[0].subSystemList = initList.ToArray();

            PlayerLoop.SetPlayerLoop(newLoop);
        }

        private bool ProcessCommandLine( List<string> args )
        {
            try
            {
                var adapterName = "";
                var startIndex = args.FindIndex(x => x == "-adapterName");
                if (startIndex >= 0)
                {
                    adapterName = args[startIndex+1];
                }
                
                startIndex = args.FindIndex(x => x == "-handshakeTimeout");
                if (startIndex >= 0)
                {
                    var timeOut = int.Parse(args[startIndex + 1]);
                    ClusterParams.RegisterTimeout = TimeSpan.FromMilliseconds(timeOut);
                }

                startIndex = args.FindIndex(x => x == "-communicationTimeout");
                if (startIndex >= 0)
                {
                    var timeOut = int.Parse(args[startIndex + 1]);
                    ClusterParams.CommunicationTimeout = TimeSpan.FromMilliseconds(timeOut);
                }

                // Process server logic
                startIndex = args.FindIndex(x => x == "-masterNode");
                if (startIndex >= 0)
                {
                    // Master command line format: -masterNode nodeId nodeCount ip:rxport,txport timeOut
                    var id = byte.Parse(args[startIndex + 1]);
                    var slaveCount = int.Parse(args[startIndex+2]);
                    var ip = args[startIndex+3].Substring(0, args[startIndex+3].IndexOf(":"));
                    var ports = args[startIndex+3].Substring(args[startIndex+3].IndexOf(":")+1);
                    var rxport = int.Parse(ports.Substring(0, ports.IndexOf(',')));
                    var txport = int.Parse(ports.Substring(ports.IndexOf(',')+1));
                    //var timeOut = int.Parse(args[startIndex+4]);
                    if (args.Count > (startIndex + 5))
                        m_Debugging = args[startIndex + 5] == "debug";

                    var master =  new MasterNode(id, slaveCount, ip, rxport, txport, 30, adapterName );
                    if (!master.Start())
                        return false;
                    stateSetter.SetIsMaster(true);
                    LocalNode = master;
                }
                

                startIndex = args.FindIndex(x => x == "-node");
                if (startIndex >= 0)
                {
                    Debug.Assert(LocalNode == null, "Dual roles not allowed.");

                    // slave command line format: -node id ip:rxport,txport timeOut
                    var id = byte.Parse(args[startIndex + 1]);
                    var ip = args[startIndex+2].Substring(0, args[startIndex+2].IndexOf(":"));
                    var ports = args[startIndex + 2].Substring(args[startIndex + 2].IndexOf(":") + 1);
                    var rxport = int.Parse(ports.Substring(0, ports.IndexOf(',')));
                    var txport = int.Parse(ports.Substring(ports.IndexOf(',') + 1));
                   // var timeOut = int.Parse(args[startIndex+3]);
                    if (args.Count > (startIndex + 4))
                        m_Debugging = args[startIndex + 4] == "debug";

                    var slave = new SlavedNode(id, ip, rxport, txport, 30, adapterName );
                    if (!slave.Start())
                        return false;
                    LocalNode = slave;
                    stateSetter.SetIsMaster(false);
                }



                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Invalid command line arguments for configuring ClusterRendering");
                Debug.LogException(e);
                return false;
            }
        }

        private void SystemUpdate()
        {
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

                    m_DelayMonitor.SampleNow();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                stateSetter.SetIsTerminated(true);
            }
            finally
            {
                if (ClusterDisplayState.IsTerminated)
                    LocalNode.Exit();
            }
        }
    }
}