using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;

namespace Unity.ClusterRendering
{
    /// <summary>
    /// Need one and only one instance of this class in the scene.
    /// it's responsible for reading the config and applying it, then invoking the
    /// node logic each frame by injecting a call back in the player loop.
    /// 
    /// Note: Allowed IPs for multi casting: 224.0.1.0 to 239.255.255.255.
    /// </summary>
    public class ClusterSynch : MonoBehaviour
    {
        [HideInInspector]
        bool m_Debugging;
        private bool m_NewFrame = true;

        /// <summary>
        /// Enables or disables the Cluster Display Synchronization. Beware that once the logic is disabled, it cannot be reenabled without restarting the application.
        /// </summary>
        [NonSerialized] bool m_ClusterLogicEnabled = false;

        [NonSerialized] private static ClusterSynch m_Instance;

        private DebugPerf m_FrameRatePerf = new DebugPerf();

        /// <summary>
        /// Returns the number of frames rendered by the Cluster Display.
        /// </summary>
        public UInt64 FrameCount => LocalNode.CurrentFrameID;

        private DebugPerf m_DelayMonitor = new DebugPerf();

        /// <summary>
        /// Getter that returns if there exists a ClusterSync instance and the synchronization has been enabled.
        /// </summary>
        public static bool Active => m_Instance != null && m_Instance.m_ClusterLogicEnabled;

        /// <summary>
        /// Returns true if the Cluster Synchronization has been terminated (a shutdown request was sent or received.)
        /// </summary>
        public static bool Terminated { get; private set; }

        /// <summary>
        /// Returns the instance of the ClusterSync singleton.
        /// </summary>
        /// <exception cref="Exception">Throws an exception if there are 2 instances present.</exception>
        public static ClusterSynch Instance
        {
            get => m_Instance;
            private set
            {
                if(m_Instance != null)
                    throw new Exception("There is pre-existing instance of MasterController!");
                m_Instance = value;
            }
        }

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
                if(m_ClusterLogicEnabled)
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

        private void OnEnable()
        {
            Terminated = false;

            m_ClusterLogicEnabled = false;
            Instance = this;
            NodeState.Debugging = m_Debugging;

            // Grab command line args related to cluster config
            var args = System.Environment.GetCommandLineArgs().ToList();
#if UNITY_EDITOR
            args = m_EditorCmdLine.Split(' ').ToList();
#endif
            var startIndex = args.FindIndex((x) => x == "-masterNode" || x == "-node");
            m_ClusterLogicEnabled = startIndex > -1;

            if (!m_ClusterLogicEnabled)
            {
                Debug.Log("ClusterRendering is missing command line configuration. Will be dormant.");
                return;
            }

            if (!ProcessCommandLine(args))
                m_ClusterLogicEnabled = false;

            if(m_ClusterLogicEnabled)
                InjectSynchPointInPlayerLoop();
        }

        private void OnDisable()
        {
            m_Instance = null;

            if (!m_ClusterLogicEnabled)
                return;

            LocalNode.Exit();

            RemoveSynchPointFromPlayerLoop();
        }

        void Update()
        {
            if (!m_ClusterLogicEnabled)
                return;

            if (Terminated)
            {
                this.enabled = false;
                m_ClusterLogicEnabled = false;
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
                x.type == typeof(UnityEngine.PlayerLoop.Initialization.PlayerUpdateTime));

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
                    var timeOut = int.Parse(args[startIndex+4]);
                    if (args.Count > (startIndex + 5))
                        m_Debugging = args[startIndex + 5] == "debug";

                    var master =  new MasterNode(id, slaveCount, ip, rxport, txport, timeOut, adapterName );
                    if (!master.Start())
                        return false;
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
                    var timeOut = int.Parse(args[startIndex+3]);
                    if (args.Count > (startIndex + 4))
                        m_Debugging = args[startIndex + 4] == "debug";

                    var slave = new SlavedNode(id, ip, rxport, txport, timeOut, adapterName );
                    if (!slave.Start())
                        return false;
                    LocalNode = slave;
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
                        Terminated = true;
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
                            Terminated = true;
                        }

                        newFrame = false;
                    } while (!LocalNode.ReadyToProceed && !Terminated);
                    m_DelayMonitor.SampleNow();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Terminated = true;
            }
            finally
            {
                if (Terminated)
                    LocalNode.Exit();
            }
        }
    }
}