using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.MissionControl.Capsule;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public interface IClusterSyncState
    {
        string InstanceName { get; }
        NodeRole NodeRole { get; set; }
        bool EmitterIsHeadless { get; }
        bool IsClusterLogicEnabled { get; }
        bool IsTerminated { get; }
        ulong Frame { get; }
        /// <summary>
        /// Assigned node ID.
        /// </summary>
        byte NodeID { get; }
        /// <summary>
        /// Adjusted node ID for rendering.
        /// </summary>
        /// <remarks>
        /// This could differ from NodeID if not all nodes perform rendering (non-rendering nodes are skipped).
        /// </remarks>
        byte RenderNodeID { get; set; }
        bool RepeatersDelayedOneFrame { get; }

        /// <summary>
        /// Updated topology of the cluster (when updated while running).
        /// </summary>
        IReadOnlyList<ChangeClusterTopologyEntry> UpdatedClusterTopology { get; set; }

        /// <summary>
        /// Event signaled when UpdatedClusterTopology is changed (most likely from another thread than the main loop).
        /// </summary>
        event Action UpdatedClusterTopologyChanged;

        string GetDiagnostics();
    }

    public partial class ClusterSync : IClusterSyncState
    {
        public NodeRole NodeRole { get; set; }
        public bool EmitterIsHeadless { get; private set; }

        public bool IsClusterLogicEnabled { get; private set; }

        public bool IsTerminated { get; private set; }
        public ulong Frame => LocalNode.FrameIndex;

        public byte NodeID => LocalNode.Config.NodeId;
        public byte RenderNodeID { get; set; }

        public bool RepeatersDelayedOneFrame { get; private set; }

        public IReadOnlyList<ChangeClusterTopologyEntry> UpdatedClusterTopology
        {
            get => m_UpdatedClusterTopology;
            set
            {
                m_UpdatedClusterTopology = value;
                UpdatedClusterTopologyChanged?.Invoke();
            }
        }
        IReadOnlyList<ChangeClusterTopologyEntry> m_UpdatedClusterTopology;
        public event Action UpdatedClusterTopologyChanged;
    }
}
