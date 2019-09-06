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
    public class Bootstrapper : MonoBehaviour
    {
        public bool m_Debugging;

        [NonSerialized]
        public bool m_ClusterLogicEnabled = false;

        [NonSerialized] private static Bootstrapper m_Instance;

        public UInt64 FrameCount { get; private set; }

        public static bool Active => m_Instance != null && m_Instance.m_ClusterLogicEnabled;

        public static bool Terminated { get; private set; }

        public static Bootstrapper Instance
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
        [NonSerialized] private List<BaseNode> m_LocalNodes = new List<BaseNode>();

        public void ShutdownAllClusterNodes()
        {
            m_LocalNodes[0].BroadcastShutdownRequest(); // matters not who triggers it
        }

        public byte ConfiguredLocalNodeId
        {
            get
            {
                if(m_ClusterLogicEnabled)
                    return m_LocalNodes[m_LocalNodes.Count-1].NodeID;
                else
                {
                    throw new Exception("Cluster Rendering not active. No local node present");
                }
            }
        }

        public byte DynamicLocalNodeId => ConfiguredLocalNodeId;

        private void OnEnable()
        {
            Terminated = false;

            m_ClusterLogicEnabled = false;
            Instance = this;
            BaseState.Debugging = m_Debugging;

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

            for( var i = 0; i < m_LocalNodes.Count; i++)
                m_LocalNodes[i].Exit();

            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();
            var entryToDel = initList.FindIndex((x) => x.type == this.GetType());
            Assert.IsTrue(entryToDel != -1, "Can't find ClusterRendering system entry in player loop");

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
            Debug.LogError("hello world " + args.Count);
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
                    m_LocalNodes.Add(master);
                }

                startIndex = args.FindIndex(x => x == "-node");
                if (startIndex >= 0)
                {
                    // slave command line format: -node id ip:rxport,txport timeOut
                    var id = byte.Parse(args[startIndex + 1]);
                    var ip = args[startIndex+2].Substring(0, args[startIndex+2].IndexOf(":"));
                    var ports = args[startIndex + 2].Substring(args[startIndex + 2].IndexOf(":") + 1);
                    var rxport = int.Parse(ports.Substring(0, ports.IndexOf(',')));
                    var txport = int.Parse(ports.Substring(ports.IndexOf(',') + 1));
                    var timeOut = int.Parse(args[startIndex+3]);

                    var slave = new SlavedNode(id, ip, rxport, txport, timeOut, m_LocalNodes.Count > 0 && m_Debugging );
                    if (!slave.Start())
                        return false;
                    m_LocalNodes.Add(slave);
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
            FrameCount++;
            try
            {
                bool frameAdvance = true;
                int nodesBusy;

                for (var i = 0; i < m_LocalNodes.Count; i++)
                    m_LocalNodes[i].NewFrameStarting();

                do
                {
                    nodesBusy = m_LocalNodes.Count == 1 ? 1 : 3; // Start with assumption that everyone is busy

                    for (var i = 0; i < m_LocalNodes.Count; i++)
                    {
                        if (!m_LocalNodes[i].DoFrame(frameAdvance))
                        {
                            // Game Over!
                            for (var j = 0; j < m_LocalNodes.Count; j++)
                                m_LocalNodes[j].Exit();

                            Terminated = true;
                            return;
                        }

                        if (m_LocalNodes[i].ReadyToProceed)
                            nodesBusy &= ~(1 << i); // Remove node from list of busy nodes
                    }

                    frameAdvance = false;

                } while (nodesBusy != 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                // Game Over!
                for (var j = 0; j < m_LocalNodes.Count; j++)
                    m_LocalNodes[j].Exit();

                Terminated = true;
                return;
            }
        }
    }
}