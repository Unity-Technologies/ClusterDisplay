using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Enum containing the Cluster node roles.
    /// </summary>
    public enum NodeRole
    {
        /// <summary>
        /// The node does not have an assigned role (not operating as part of a cluster).
        /// </summary>
        Unassigned,
        /// <summary>
        /// The source node that broadcasts synchronization data.
        /// </summary>
        Emitter,
        /// <summary>
        /// The client nodes that receive synchronization data.
        /// </summary>
        Repeater
    }

    public interface IClusterSyncState
    {
        NodeRole NodeRole { get; }
        bool EmitterIsHeadless { get; }
        bool IsClusterLogicEnabled { get; }
        bool IsTerminated { get; }
        ulong Frame { get; }
        /// <summary>
        /// Assigned node ID.
        /// </summary>
        ushort NodeID { get; }
        /// <summary>
        /// Adjusted node ID for rendering.
        /// </summary>
        /// <remarks>
        /// This could differ from NodeID if not all nodes perform rendering (non-rendering nodes are skipped).
        /// </remarks>
        ushort RenderNodeID { get; }
        bool RepeatersDelayedOneFrame { get; }
        bool ReplaceHeadlessEmitter { get; }

        string GetDiagnostics();
    }

    partial class ClusterSync : IClusterSyncState
    {
        public NodeRole NodeRole { get; private set; }
        public bool EmitterIsHeadless { get; private set; }

        public bool IsClusterLogicEnabled { get; private set; }

        public bool IsTerminated { get; private set; }
                public ulong Frame => LocalNode != null ? LocalNode.CurrentFrameID : 0;

        public ushort NodeID => (ushort)(LocalNode != null ? LocalNode.NodeID : 0);
        public ushort RenderNodeID =>
            ReplaceHeadlessEmitter && NodeRole is NodeRole.Repeater
                ? (ushort)(NodeID - 1)
                : NodeID;

        public bool RepeatersDelayedOneFrame { get; private set; }
        public bool ReplaceHeadlessEmitter { get; private set; }
    }
}
