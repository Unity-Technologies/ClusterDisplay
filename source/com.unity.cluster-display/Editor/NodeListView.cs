using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl
{
    class NodeListView : TreeView
    {
        public class NodeViewItem : TreeViewItem
        {
            public NodeViewItem(ExtendedNodeInfo extendedNodeInfo) : base(extendedNodeInfo.Id, 0)
            {
                NodeInfo = extendedNodeInfo;
            }
            
            public ExtendedNodeInfo NodeInfo { get; set; }
            
            public bool IsActive { get; set; } = true;

            public int ClusterId { get; set; } = 0;
        }
        
        const int k_RootId = 0;
        const int k_HiddenRootDepth = -1;

        readonly Dictionary<int, NodeViewItem> m_AvailableNodes = new();

        public IEnumerable<NodeViewItem> Nodes => m_AvailableNodes.Values;

        public NodeListView(TreeViewState state, MultiColumnHeader multiColumnHeader)
            : base(state, multiColumnHeader)
        {
        }

        enum Column
        {
            IsActive,
            NodeId,
            HostName,
            IPAddress,
            Status
        }

        public static MultiColumnHeaderState CreateHeaderState()
        {
            return new MultiColumnHeaderState(new []
            {
                new MultiColumnHeaderState.Column
                {
                    width = 50, 
                    minWidth = 50,
                    headerContent = new GUIContent("Use?")
                },
                new MultiColumnHeaderState.Column
                {
                    width = 50, 
                    minWidth = 50,
                    headerContent = new GUIContent("ID")
                },
                new MultiColumnHeaderState.Column
                {
                    width = 150, 
                    minWidth = 60,
                    headerContent = new GUIContent("Host name")
                },
                new MultiColumnHeaderState.Column
                {
                    width = 150, 
                    minWidth = 60,
                    headerContent = new GUIContent("Address")
                },
                new MultiColumnHeaderState.Column
                {
                    width = 150, 
                    minWidth = 60,
                    headerContent = new GUIContent("Status")
                }
            });
        }

        protected override TreeViewItem BuildRoot()
        {
            return new TreeViewItem(k_RootId, k_HiddenRootDepth);
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = m_AvailableNodes.Values.Select(item => item as TreeViewItem).ToList();
            
            SetupParentsAndChildrenFromDepths(root, rows);

            return rows;
        }

        public void AddItem(ExtendedNodeInfo node)
        {
            m_AvailableNodes.Add(node.Id, new NodeViewItem(node));
            Reload();
        }

        public void UpdateItem(ExtendedNodeInfo node)
        {
            m_AvailableNodes[node.Id].NodeInfo = node;
            Reload();
        }

        public void RemoveItem(ExtendedNodeInfo node)
        {
            m_AvailableNodes.Remove(node.Id);
            Reload();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                var item = args.item as NodeViewItem;
                if (item == null) continue;
                
                var rect = args.GetCellRect(i);
                var column = (Column)args.GetColumn(i);
                switch (column)
                {
                    case Column.IsActive:
                        item.IsActive = GUI.Toggle(rect, item.IsActive, GUIContent.none);
                        break;
                    case Column.NodeId:
                        item.ClusterId = EditorGUI.IntField(rect, item.ClusterId);
                        break;
                    case Column.HostName:
                        GUI.Label(rect, item.NodeInfo.Name);
                        break;
                    case Column.IPAddress:
                        GUI.Label(rect, $"{item.NodeInfo.Address}:{item.NodeInfo.Port}");
                        break;
                    case Column.Status:
                        GUI.Label(rect, $"{item.NodeInfo.Info.Status}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}