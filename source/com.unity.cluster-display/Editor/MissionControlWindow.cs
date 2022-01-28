using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;

namespace Unity.ClusterDisplay.MissionControl.Editor
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
        bool m_ClearRegistry = false;

        [SerializeField]
        Vector2 m_ListScrollPos;

        [SerializeField]
        string m_BroadcastProxyAddress;

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
        CancellationTokenSource m_GeneralCancellationTokenSource;

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

            if (m_ServerTask.IsFaulted) { }
        }

        void OnDisable()
        {
            m_GeneralCancellationTokenSource.Cancel();
            m_LaunchCancellationTokenSource?.Cancel();
            m_Server?.Dispose();
            m_Server = null;
        }

        void OnEnable()
        {
            m_TreeViewState ??= new TreeViewState();
            m_MultiColumnHeaderState ??= NodeListView.CreateHeaderState();

            var header = new MultiColumnHeader(m_MultiColumnHeaderState);

            m_NodeListView = new NodeListView(m_TreeViewState, header);
            m_NodeListView.Reload();

            m_GeneralCancellationTokenSource = new CancellationTokenSource();

            CreatePLayerList();
            RefreshPlayers(m_GeneralCancellationTokenSource.Token)
                .WithErrorHandling(LogException);

            InitializeServer();
        }

        void InitializeServer()
        {
            m_Server?.Dispose();
            if (string.IsNullOrEmpty(m_BroadcastProxyAddress))
            {
                m_Server = new Server();
            }
            else
            {
                m_Server = new Server(0,
                    new IPEndPoint(IPAddress.Parse(m_BroadcastProxyAddress),
                        Constants.BroadcastProxyPort));
            }

            m_Server.Run().WithErrorHandling(LogException);
            m_Server.NodeUpdated += m_NodeListView.UpdateItem;
            m_Server.NodeAdded += m_NodeListView.AddItem;
            m_Server.NodeRemoved += m_NodeListView.RemoveItem;
        }

        static void LogException(Exception ex)
        {
            Debug.Log(ex.Message);
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

        async Task RefreshPlayers(CancellationToken token)
        {
            if (string.IsNullOrEmpty(m_RootPath))
            {
                return;
            }

            m_PlayerList.Clear();
            var apps = await Task.Run(
                () => FolderUtils.ListPlayers(m_RootPath).ToList(),
                token);
            m_PlayerList.AddRange(apps);
        }

        void OnGUI()
        {
            EditorGUIUtility.wideMode = true;
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                m_BroadcastProxyAddress = EditorGUILayout.DelayedTextField(new GUIContent(
                        "Broadcast Proxy Address",
                        "If you are unable to broadcast to the cluster, enter the address of a node and discovery messages will be rebroadcasted to the cluster"),
                    m_BroadcastProxyAddress);

                if (check.changed)
                {
                    InitializeServer();
                }
            }

            var rect = GUILayoutUtility.GetRect(0, position.width, 0, position.height);
            m_NodeListView?.OnGUI(rect);

            using (new EditorGUILayout.HorizontalScope())
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var rootPath = EditorGUILayout.DelayedTextField(new GUIContent("Shared Folder"), m_RootPath);

                if (check.changed || GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    m_RootPath = rootPath;
                    if (!string.IsNullOrWhiteSpace(rootPath))
                    {
                        RefreshPlayers(m_GeneralCancellationTokenSource.Token).WithErrorHandling(LogException);
                    }
                }
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ListScrollPos))
            {
                m_ListScrollPos = scrollView.scrollPosition;
                m_ProjectListView.DoLayoutList();
            }

            m_HandshakeTimeout = EditorGUILayout.IntField("Handshake Timeout", m_HandshakeTimeout);
            m_CommTimeout = EditorGUILayout.IntField("Communication Timeout", m_CommTimeout);
            m_ClearRegistry = EditorGUILayout.Toggle(new GUIContent("Delete registry key",
                    "Enable if you are having trouble running in Exclusive Fullscreen"),
                m_ClearRegistry);

            if (GUILayout.Button("Run Selected Player") && m_ProjectListView.index >= 0 && m_PlayerList.Count > m_ProjectListView.index)
            {
                var selectedPlayerDir = Path.Combine(m_RootPath, m_PlayerList[m_ProjectListView.index].DirectoryName);
                var activeNodes = m_NodeListView?.Nodes.Where(x => x.IsActive).ToList()
                    ?? new List<NodeListView.NodeViewItem>();
                var numRepeaters = activeNodes.Count - 1;
                var launchData = activeNodes
                    .Select(x => (x.NodeInfo, new LaunchInfo(selectedPlayerDir,
                        x.ClusterId,
                        numRepeaters,
                        m_ClearRegistry,
                        m_HandshakeTimeout,
                        m_CommTimeout)));
                m_LaunchCancellationTokenSource = new CancellationTokenSource();

                m_Server.SyncAndLaunch(launchData, m_LaunchCancellationTokenSource.Token).WithErrorHandling(LogException);
            }

            if (GUILayout.Button("Stop All"))
            {
                m_LaunchCancellationTokenSource?.Cancel();
                m_Server.StopAll().WithErrorHandling(LogException);
            }
        }
    }
}
