using System;
using System.Collections.Generic;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Describe the new role of a node in the cluster.
    /// </summary>
    public struct ClusterTopologyEntry
    {
        /// <summary>
        /// NodeId of the node this entry describes.
        /// </summary>
        public byte NodeId;
        /// <summary>
        /// Role of the node.
        /// </summary>
        public NodeRole NodeRole;
        /// <summary>
        /// RenderNodeId of the node (might be different from NodeId).
        /// </summary>
        public byte RenderNodeId;
    }

    /// <summary>
    /// List (with event when the list is changed) of <see cref="ClusterTopologyEntry"/>.
    /// </summary>
    public class ClusterTopology
    {
        /// <summary>
        /// List of <see cref="ClusterTopologyEntry"/>.
        /// </summary>
        public IReadOnlyList<ClusterTopologyEntry> Entries
        {
            get => m_Entries;
            set
            {
                m_Entries = value;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Event signaled when <see cref="Entries"/> is changed (most likely from another thread than the main loop).
        /// </summary>
        public event Action Changed;

        IReadOnlyList<ClusterTopologyEntry> m_Entries;
    }
}
