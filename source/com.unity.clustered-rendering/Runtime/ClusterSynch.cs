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
    /// </summary>
    public class ClusterSynch : MonoBehaviour
    {
        public bool m_Debugging;
        private bool m_NewFrame = true;

        [NonSerialized]
        public bool m_ClusterLogicEnabled = false;

        [NonSerialized] private static ClusterSynch m_Instance;

        public UInt64 FrameCount => LocalNode.CurrentFrameID;

        public static bool Active => m_Instance != null && m_Instance.m_ClusterLogicEnabled;

        public static bool Terminated { get; private set; }

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

        public NetworkingStats CurrentNetworkStats => LocalNode.UdpAgent.CurrentNetworkStats;

        public void ShutdownAllClusterNodes()
        {
            LocalNode.BroadcastShutdownRequest(); // matters not who triggers it
        }

        public byte ConfiguredLocalNodeId
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

        public byte DynamicLocalNodeId => ConfiguredLocalNodeId;

        public string GetDebugString()
        {
            return LocalNode.GetDebugString();
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
                EditPlayerLoop();
        }

        private void OnDisable()
        {
            m_Instance = null;

            if (!m_ClusterLogicEnabled)
                return;

            LocalNode.Exit();

            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            var entryToDel = initList.FindIndex((x) => x.type == this.GetType());

            initList.RemoveAt(entryToDel);

            newLoop.subSystemList[0].subSystemList = initList.ToArray();

            PlayerLoop.SetPlayerLoop(newLoop);
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

        private void EditPlayerLoop()
        {
            // Inject into player loop
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            var indexOfTimeSlice = initList.FindIndex((x) =>
                x.type == typeof(UnityEngine.PlayerLoop.Initialization.AsyncUploadTimeSlicedUpdate));

            Assert.IsFalse(initList.Any((x) => x.type == this.GetType()), "Player loop already has a ClusterRendering system entry registered.");
            Assert.IsTrue(indexOfTimeSlice != -1, "Can't find insertion point in player loop for ClusterRendering system");

            initList.Insert(indexOfTimeSlice + 1, new PlayerLoopSystem()
            {
                type = this.GetType(),
                updateDelegate = SystemUpdate
            });


            newLoop.subSystemList[0].subSystemList = initList.ToArray();

            PlayerLoop.SetPlayerLoop(newLoop);

        }

        private bool ProcessCommandLine( List<string> args )
        {
            try
            {
                // Process server logic
                var startIndex = args.FindIndex(x => x == "-masterNode");
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

                    var master =  new MasterNode(id, slaveCount, ip, rxport, txport, timeOut );
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

                    var slave = new SlavedNode(id, ip, rxport, txport, timeOut );
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
                    if (!LocalNode.DoFrame(m_NewFrame))
                    {
                        // Game Over!
                        Terminated = true;
                    }

                    m_NewFrame = LocalNode.ReadyToProceed;
                }
                else
                {
                    var newFrame = true;
                    do
                    {
                        if (!LocalNode.DoFrame(newFrame))
                        {
                            // Game Over!
                            Terminated = true;
                        }

                        newFrame = false;
                    } while (!LocalNode.ReadyToProceed && !Terminated);
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