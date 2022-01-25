using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.ClusterDisplay.MissionControl;
using Unity.EditorCoroutines.Editor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;

namespace Unity.ClusterDisplay.Editor
{
    class MissionControlWindow : EditorWindow
    {
        Server m_Server;
        NodeListView m_NodeListView;
        ReorderableList m_ProjectListView;

        [SerializeField]
        string m_RootPath;

        [SerializeField]
        int m_HandshakeTimeout = 10000;

        [SerializeField]
        int m_CommTimeout = 5000;

        [SerializeField]
        Vector2 m_ListScrollPos;

        List<PlayerInfo> m_PlayerList = new();
        Task m_ServerTask;

        /// <summary>
        /// Serialized fields get written out to the window layout file.
        /// </summary>
        [SerializeField]
        TreeViewState m_TreeViewState;

        [SerializeField]
        MultiColumnHeaderState m_MultiColumnHeaderState;

        CancellationTokenSource m_LaunchCancellationTokenSource;

        [MenuItem("Cluster Display/Mission Control")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissionControlWindow>();
            window.titleContent = new GUIContent("Cluster Display Mission Control");
            window.Show();
        }

        void Update()
        {
            if (m_ServerTask == null)
            {
                return;
            }

            if (m_ServerTask.IsFaulted)
            {
                
            }
        }

        void OnDisable()
        {
            m_Server?.Dispose();
            m_Server = null;
        }

        void OnEnable()
        {
            m_TreeViewState ??= new TreeViewState();
            m_MultiColumnHeaderState ??= NodeListView.CreateHeaderState();

            var header = new MultiColumnHeader(m_MultiColumnHeaderState);

            m_Server = new Server();

            m_NodeListView = new NodeListView(m_TreeViewState, header);
            m_NodeListView.Reload();

            this.StartCoroutine(m_Server.Run().ToCoroutine(e => Debug.Log(e.Message)));
            m_Server.NodeUpdated += m_NodeListView.UpdateItem;
            m_Server.NodeAdded += m_NodeListView.AddItem;
            m_Server.NodeRemoved += m_NodeListView.RemoveItem;

            CreatePLayerList();
            RefreshPlayers();
        }

        void CreatePLayerList()
        {
            m_ProjectListView = new ReorderableList(m_PlayerList, typeof(string))
            {
                draggable = false,
                displayAdd = false,
                displayRemove = false,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Available Players"),
                drawElementCallback = (rect, index, _, _) => EditorGUI.LabelField(rect, m_PlayerList[index].ToString())
            };
        }

        async void RefreshPlayers()
        {
            if (string.IsNullOrEmpty(m_RootPath))
            {
                return;
            }
            
            m_PlayerList.Clear();
            var apps = await Task.Run(() => FolderUtils.ListPlayers(m_RootPath));
            m_PlayerList.AddRange(apps);
        }

        void OnGUI()
        {
            EditorGUIUtility.wideMode = true;
            var rect = GUILayoutUtility.GetRect(0, position.width, 0, position.height);
            m_NodeListView?.OnGUI(rect);

            var rootPath = EditorGUILayout.TextField(new GUIContent("Shared Folder"), m_RootPath);

            if (GUI.changed)
            {
                m_RootPath = rootPath;
                m_PlayerList.Clear();
                if (!string.IsNullOrWhiteSpace(rootPath))
                {
                    RefreshPlayers();
                }
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ListScrollPos))
            {
                m_ListScrollPos = scrollView.scrollPosition;
                m_ProjectListView.DoLayoutList();
            }

            m_HandshakeTimeout = EditorGUILayout.IntField("Handshake Timeout", m_HandshakeTimeout);
            m_CommTimeout = EditorGUILayout.IntField("Communication Timeout", m_CommTimeout);

            if (GUILayout.Button("Run Selected Player") && m_ProjectListView.index >= 0 && m_PlayerList.Count > m_ProjectListView.index)
            {
                var selectedPlayerDir = Path.Combine(m_RootPath, m_PlayerList[m_ProjectListView.index].DirectoryName);
                var activeNodes = m_NodeListView?.Nodes.Where(x => x.IsActive).ToList()
                    ?? new List<NodeListView.NodeViewItem>();
                var numRepeaters = activeNodes.Count - 1;
                var launchData = activeNodes
                    .Select(x => (x.NodeInfo, new LaunchInfo(selectedPlayerDir, x.ClusterId, numRepeaters)));
                m_LaunchCancellationTokenSource = new CancellationTokenSource();
                
                this.StartCoroutine(m_Server.Launch(launchData, m_LaunchCancellationTokenSource.Token)
                        .ToCoroutine(e => Debug.Log(e.Message)));
            }

            if (GUILayout.Button("Stop All"))
            {
                m_LaunchCancellationTokenSource?.Cancel();
                m_Server.StopAll();
            }
        }
    }
}
