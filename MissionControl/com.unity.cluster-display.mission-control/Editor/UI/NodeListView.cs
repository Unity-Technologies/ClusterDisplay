using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.MissionControl.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.ClusterDisplay.MissionControl
{
    class NodeListView : TreeView
    {
        internal class NodeViewItem : TreeViewItem
        {
            public NodeViewItem(ExtendedNodeInfo extendedNodeInfo,
                MutableNodeSettings settings)
                : base(extendedNodeInfo.Id,
                    0)
            {
                NodeInfo = extendedNodeInfo;
                MutableSettings = settings;
            }

            public ExtendedNodeInfo NodeInfo { get; set; }

            public MutableNodeSettings MutableSettings;
        }

        class IdComparer : IEqualityComparer<ExtendedNodeInfo>
        {
            public bool Equals(ExtendedNodeInfo x, ExtendedNodeInfo y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode(ExtendedNodeInfo obj)
            {
                return obj.Id;
            }
        }

        const int k_RootId = 0;
        const int k_HiddenRootDepth = -1;

        readonly Dictionary<int, NodeViewItem> m_AvailableNodes = new();
        bool m_Dirty = true;

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

        public override void OnGUI(Rect rect)
        {
            if (m_Dirty)
            {
                Reload();
                m_Dirty = false;
            }
            base.OnGUI(rect);
        }

        public void RefreshNodes(IEnumerable<ExtendedNodeInfo> activeNodes)
        {
            var target = activeNodes.ToList();
            var displayed = m_AvailableNodes.Values.Select(x => x.NodeInfo).ToList();
            var existing = displayed.Intersect(target, new IdComparer()).ToList();
            var added = target.Except(existing, new IdComparer());
            var removed = displayed.Except(existing, new IdComparer());

            foreach (var node in existing)
            {
                UpdateItem(node);
            }

            foreach (var newNode in added)
            {
                AddItem(newNode);
            }

            foreach (var oldNode in removed)
            {
                RemoveItem(oldNode);
            }
        }

        public void AddItem(ExtendedNodeInfo node)
        {
            var cachedSettings = MissionControlSettings.instance.NodeSettings.FirstOrDefault(ns => ns.Id == node.Id);
            if (cachedSettings.Id == 0)
            {
                cachedSettings = new MutableNodeSettings(node.Id);
            }

            m_AvailableNodes.Add(node.Id, new NodeViewItem(node, cachedSettings));
            m_Dirty = true;
        }

        public void UpdateItem(ExtendedNodeInfo node)
        {
            var changed = m_AvailableNodes[node.Id].NodeInfo != node;
            m_AvailableNodes[node.Id].NodeInfo = node;
            m_Dirty = m_Dirty || changed;
        }

        public void RemoveItem(ExtendedNodeInfo node)
        {
            m_AvailableNodes.Remove(node.Id);
            m_Dirty = true;
        }

        public void RemoveAllItems()
        {
            if (m_AvailableNodes.Count > 0)
            {
                m_AvailableNodes.Clear();
                m_Dirty = true;
            }
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
                        item.MutableSettings.IsActive = GUI.Toggle(rect,
                            item.MutableSettings.IsActive,
                            GUIContent.none);
                        break;
                    case Column.NodeId:
                        item.MutableSettings.ClusterId = EditorGUI.IntField(rect, item.MutableSettings.ClusterId);
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
