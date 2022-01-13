using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.ClusterDisplay.MissionControl;
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
        
        List<string> m_ProjectDirs = new();
        Task m_ServerTask;

        /// <summary>
        /// Serialized fields get written out to the window layout file.
        /// </summary>
        [SerializeField]
        TreeViewState m_TreeViewState;

        [SerializeField]
        MultiColumnHeaderState m_MultiColumnHeaderState;

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

            m_Server?.Dispose();
            m_Server = new Server();

            m_NodeListView = new NodeListView(m_TreeViewState, header);
            m_NodeListView.Reload();

            m_ServerTask = m_Server.Run();
            m_Server.NodeUpdated += m_NodeListView.UpdateItem;
            m_Server.NodeAdded += m_NodeListView.AddItem;
            m_Server.NodeRemoved += m_NodeListView.RemoveItem;
            
            CreateProjectList();
        }

        void CreateProjectList()
        {
            m_ProjectListView = new ReorderableList(m_ProjectDirs, typeof(string))
            {
                draggable = false,
                displayAdd = false,
                displayRemove = false,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Available projects")
            };
        }

        async Task RefreshFolders()
        {
            var dirInfo = new DirectoryInfo(m_RootPath);
            m_ProjectDirs.Clear();
            var results = await Task.Run(() => dirInfo.GetDirectories().Select(folder => folder.Name).ToList());
            m_ProjectDirs.AddRange(results);
        }

        void OnGUI()
        {
            EditorGUIUtility.wideMode = true;
            var rect = GUILayoutUtility.GetRect(0, position.width, 0, position.height);
            m_NodeListView?.OnGUI(rect);
            
            var rootPath = EditorGUILayout.TextField(new GUIContent("Project Root"), m_RootPath);

            if (GUI.changed)
            {
                m_RootPath = rootPath;
                m_ProjectDirs.Clear();
                if (!string.IsNullOrWhiteSpace(rootPath))
                {
                    RefreshFolders();
                }
            }
            
            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ListScrollPos))
            {
                m_ListScrollPos = scrollView.scrollPosition;
                m_ProjectListView.DoLayoutList();
            }

            m_HandshakeTimeout = EditorGUILayout.IntField("Handshake Timeout", m_HandshakeTimeout);
            m_CommTimeout = EditorGUILayout.IntField("Communication Timeout", m_CommTimeout);

            if (GUILayout.Button("Run selected project") && m_ProjectListView.index >= 0 && m_ProjectDirs.Count > m_ProjectListView.index)
            {
                Debug.Log("Launch");
                var selectedProject = Path.Combine(m_RootPath, m_ProjectDirs[m_ProjectListView.index]);
                var activeNodes = m_NodeListView?.Nodes.Where(x => x.IsActive).ToList()
                    ?? new List<NodeListView.NodeViewItem>();
                var numRepeaters = activeNodes.Count - 1;
                var launchData = activeNodes
                    .Select(x => (x.NodeInfo, new LaunchInfo(selectedProject, x.ClusterId, numRepeaters)));
                _ = m_Server.Launch(launchData);
            }

            if (GUILayout.Button("Stop All"))
            {
                m_Server.StopAll();
            }
        }
    }
}
