using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public interface IClusterSyncState
    {
        string InstanceName { get; }
        NodeRole NodeRole { get; }
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

        string GetDiagnostics();

        /// <summary>
        /// Request a change in the topology of the cluster to be done as soon as possible.
        /// </summary>
        /// <param name="entries">Entries describing the new topology.</param>
        /// <remarks>This method can be called from any thread and need to be called with the same topology for all the
        /// nodes of the cluster.  There are a few important thing to remember in deciding the topology of the cluster:
        /// <ul>
        /// <li>A node can only transition from <see cref="ClusterDisplay.NodeRole.Backup"/> to
        /// <see cref="ClusterDisplay.NodeRole.Emitter"/> or <see cref="ClusterDisplay.NodeRole.Repeater"/>.</li>
        /// <li>There can be only one <see cref="ClusterDisplay.NodeRole.Emitter"/> node.</li>
        /// <li>A node id missing in <paramref name="entries"/> indicate that the node is to be removed from the
        /// cluster.</li>
        /// <li>A node cannot be added back to the cluster once it has been removed.</li>
        /// </ul>
        /// </remarks>
        void ChangeClusterTopology(IReadOnlyList<ClusterTopologyEntry> entries);
    }

    public partial class ClusterSync : IClusterSyncState
    {
        public NodeRole NodeRole => LocalNode?.NodeRole ?? NodeRole.Unassigned;

        public bool EmitterIsHeadless { get; private set; }

        public bool IsClusterLogicEnabled { get; private set; }

        public bool IsTerminated { get; private set; }
        public ulong Frame => LocalNode.FrameIndex;

        public byte NodeID => LocalNode.Config.NodeId;

        public byte RenderNodeID
        {
            get => LocalNode.RenderNodeId;
            set
            {
                if (LocalNode != null) { LocalNode.RenderNodeId = value; }
            }
        }

        public bool RepeatersDelayedOneFrame { get; private set; }

        public void ChangeClusterTopology(IReadOnlyList<ClusterTopologyEntry> entries)
        {
            int emitterCount = 0;
            foreach (var entry in entries)
            {
                if (entry.NodeRole == NodeRole.Emitter)
                {
                    ++emitterCount;
                }
            }

            if (emitterCount != 1)
            {
                throw new ArgumentException($"There should be one (no more, no less) " +
                    $"{nameof(NodeRole.Emitter)} in {nameof(entries)}.", nameof(entries));
            }
            LocalNode.UpdatedClusterTopology.Entries = entries;
        }
    }
}
