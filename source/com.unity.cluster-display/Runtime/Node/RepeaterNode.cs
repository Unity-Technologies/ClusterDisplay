using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.RepeaterStateMachine;
#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Class for the objects representing a repeater cluster node (a node receiving its state an emitter node).
    /// </summary>
    class RepeaterNode : ClusterNode
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Node's configuration.</param>
        /// <param name="udpAgent">Object through which we will perform communication with other nodes in the cluster.
        /// Must support reception of <see cref="ReceiveMessageTypes"/>.</param>
        /// <param name="isBackup">Is the repeater node used to perform the work of a backup node?</param>
        public RepeaterNode(ClusterNodeConfig config, IUdpAgent udpAgent, bool isBackup = false)
            : base(config, udpAgent)
        {
            NodeRole = isBackup ? NodeRole.Backup : NodeRole.Repeater;
            SetInitialState(Config.Fence is FrameSyncFence.Hardware ?
                HardwareSyncInitState.Create(this) : new RegisterWithEmitterState(this));
        }

        /// <inheritdoc />
        public override void DoFrame()
        {
            ProcessTopologyChanges();
            base.DoFrame();
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        /// <summary>
        /// Overridable method called to trigger quit of the application / game
        /// </summary>
        public virtual void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Process cluster topology changes
        /// </summary>
        void ProcessTopologyChanges()
        {
            if (UpdatedClusterTopology.Entries != null &&
                (!m_LastAnalyzedTopology.TryGetTarget(out var lastAnalyzedTopology) ||
                 !ReferenceEquals(lastAnalyzedTopology, UpdatedClusterTopology.Entries)))
            {
                HandleChangesInNodeAssignment(UpdatedClusterTopology.Entries);
                m_LastAnalyzedTopology.SetTarget(UpdatedClusterTopology.Entries);
            }
        }

        /// <summary>
        /// Check if the role or render node id of this node has changed.
        /// </summary>
        void HandleChangesInNodeAssignment(IReadOnlyList<ClusterTopologyEntry> entries)
        {
            // Find entry for this node
            ClusterTopologyEntry? thisNodeEntry = null;
            foreach (var entry in entries)
            {
                if (entry.NodeId == Config.NodeId)
                {
                    thisNodeEntry = entry;
                    break;
                }
            }
            if (!thisNodeEntry.HasValue)
            {
                // No entry means the node has been removed from the cluster, let's quit to avoid using power for
                // nothing...
                Quit();
                return;
            }

            NodeRole = thisNodeEntry.Value.NodeRole;
            RenderNodeId = thisNodeEntry.Value.RenderNodeId;
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RepeaterRegistered,
            MessageType.FrameData, MessageType.EmitterWaitingToStartFrame, MessageType.PropagateQuit,
            MessageType.QuadroBarrierWarmupHeartbeat};

        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ClusterTopologyEntry>> m_LastAnalyzedTopology = new(null);
    }
}
