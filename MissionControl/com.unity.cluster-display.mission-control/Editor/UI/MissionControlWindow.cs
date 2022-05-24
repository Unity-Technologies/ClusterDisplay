using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.Editor
{
    class MissionControlWindow : EditorWindow
    {
        Server m_Server;
        NodeListView m_NodeListView;
        ReorderableList m_ProjectListView;

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

            SaveSettings();
        }

        void OnDestroy()
        {
            OnDisable();
        }

        void OnEnable()
        {
            m_TreeViewState ??= new TreeViewState();
            m_MultiColumnHeaderState ??= NodeListView.CreateHeaderState();

            var header = new MultiColumnHeader(m_MultiColumnHeaderState);

            m_NodeListView = new NodeListView(m_TreeViewState, header);

            m_GeneralCancellationTokenSource = new CancellationTokenSource();

            CreatePLayerList();
            _ = RefreshPlayers(m_GeneralCancellationTokenSource.Token)
                .WithErrorHandling(LogException);

            InitializeServer();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(10000);
                    if (m_Server != null)
                    {
                        m_NodeListView.RefreshNodes(m_Server.Nodes);
                    }
                }
            }, m_GeneralCancellationTokenSource.Token)
                .WithErrorHandling(LogException);
        }

        void InitializeServer()
        {
            var settings = MissionControlSettings.instance;
            m_Server?.Dispose();
            m_NodeListView.RemoveAllItems();
            if (string.IsNullOrEmpty(settings.BroadcastProxyAddress))
            {
                m_Server = new Server(settings.NetworkAdapterName);
            }
            else
            {
                m_Server = new Server(settings.NetworkAdapterName, 0,
                    new IPEndPoint(IPAddress.Parse(settings.BroadcastProxyAddress),
                        Constants.BroadcastProxyPort));
            }

            m_Server.NodeUpdated += m_NodeListView.UpdateItem;
            m_Server.NodeAdded += m_NodeListView.AddItem;
            m_Server.NodeRemoved += m_NodeListView.RemoveItem;
            _ = m_Server.Run().WithErrorHandling(LogException);
        }

        /// <summary>
        /// Serializes the settings to disk.
        /// </summary>
        /// <remarks>
        /// The data file is stored in the UserSettings folder of the project.
        /// </remarks>
        void SaveSettings()
        {
            var tempDictionary = MissionControlSettings.instance.NodeSettings.ToDictionary(ns => ns.Id);
            foreach (var nodeView in m_NodeListView.Nodes)
            {
                tempDictionary[nodeView.id] = nodeView.MutableSettings;
            }

            MissionControlSettings.instance.NodeSettings = tempDictionary.Values.ToList();
            MissionControlSettings.instance.Save();
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
            if (string.IsNullOrEmpty(MissionControlSettings.instance.RootPath))
            {
                return;
            }

            m_PlayerList.Clear();
            var apps = await Task.Run(
                () => FolderUtils.ListPlayers(MissionControlSettings.instance.RootPath).ToList(),
                token);
            m_PlayerList.AddRange(apps);
        }

        void OnGUI()
        {
            var settings = MissionControlSettings.instance;

            var localAdapters = LocalNetworkAdapter.BuildList(true, settings.NetworkAdapterName);
            int previousChoiceIndx = LocalNetworkAdapter.IndexOfInList(localAdapters, settings.NetworkAdapterName);
            Debug.Assert(previousChoiceIndx >= 0);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                int selectedIndex = EditorGUILayout.Popup(new GUIContent("Adapter name"), previousChoiceIndx,
                    localAdapters.Select(a => a.DisplayName).ToArray());
                if (check.changed)
                {
                    settings.NetworkAdapterName = localAdapters[selectedIndex].Name;
                    InitializeServer();
                }
            }

            EditorGUIUtility.wideMode = true;
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                settings.BroadcastProxyAddress = EditorGUILayout.DelayedTextField(new GUIContent(
                        "Broadcast Proxy Address",
                        "If you are unable to broadcast to the cluster, enter the address of a node and discovery messages will be rebroadcasted to the cluster"),
                    settings.BroadcastProxyAddress);

                if (check.changed)
                {
                    InitializeServer();
                }
            }

            var rect = GUILayoutUtility.GetRect(0, position.width, 0, position.height / 4);
            m_NodeListView?.OnGUI(rect);

            using (new EditorGUILayout.HorizontalScope())
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUIUtility.labelWidth = 100;
                var rootPath = EditorGUILayout.DelayedTextField(new GUIContent("Shared Folder"), settings.RootPath);

                if (check.changed || GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    settings.RootPath = rootPath;
                    if (!string.IsNullOrWhiteSpace(rootPath))
                    {
                        _ = RefreshPlayers(m_GeneralCancellationTokenSource.Token).WithErrorHandling(LogException);
                    }
                }
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ListScrollPos))
            {
                m_ListScrollPos = scrollView.scrollPosition;
                m_ProjectListView.DoLayoutList();
            }

            EditorGUIUtility.labelWidth = 200;
            settings.HandshakeTimeout = EditorGUILayout.IntField("Handshake Timeout", settings.HandshakeTimeout);
            settings.Timeout = EditorGUILayout.IntField("Communication Timeout", settings.Timeout);
            settings.DeleteRegistryKey = EditorGUILayout.Toggle(new GUIContent("Delete registry key",
                    "Enable if you are having trouble running in Exclusive Fullscreen"),
                settings.DeleteRegistryKey);
            settings.UseDeprecatedArgs = EditorGUILayout.Toggle("Use deprecated command line", settings.UseDeprecatedArgs);
            settings.ExtraArgs = EditorGUILayout.TextField("Extra command line arguments", settings.ExtraArgs);

            if (GUILayout.Button("Run Selected Player") && m_ProjectListView.index >= 0 && m_PlayerList.Count > m_ProjectListView.index)
            {
                var selectedPlayerDir = Path.Combine(settings.RootPath, m_PlayerList[m_ProjectListView.index].DirectoryName);
                var activeNodes = m_NodeListView?.Nodes.Where(x => x.MutableSettings.IsActive).ToList()
                    ?? new List<NodeListView.NodeViewItem>();
                var numRepeaters = activeNodes.Count - 1;
                var launchData = activeNodes
                    .Select(x => (x.NodeInfo, new LaunchInfo(selectedPlayerDir,
                        x.MutableSettings.ClusterId,
                        numRepeaters,
                        settings.DeleteRegistryKey,
                        settings.HandshakeTimeout,
                        settings.Timeout,
                        settings.ExtraArgs,
                        settings.UseDeprecatedArgs)));
                m_LaunchCancellationTokenSource = new CancellationTokenSource();

                _ = m_Server.SyncAndLaunch(launchData, m_LaunchCancellationTokenSource.Token).WithErrorHandling(LogException);
            }

            if (GUILayout.Button("Stop All"))
            {
                m_LaunchCancellationTokenSource?.Cancel();
                _ = m_Server.StopAll(m_GeneralCancellationTokenSource.Token).WithErrorHandling(LogException);
            }

            if (GUI.changed)
            {
                SaveSettings();
            }
        }
    }
}
