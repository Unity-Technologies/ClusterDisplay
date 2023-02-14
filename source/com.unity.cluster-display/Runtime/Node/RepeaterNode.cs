using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.RepeaterStateMachine;
using Unity.ClusterDisplay.Utils;
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
        public RepeaterNode(ClusterNodeConfig config, IUdpAgent udpAgent)
            : base(config, udpAgent)
        {
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
            if (!ServiceLocator.TryGet<IClusterSyncState>(out var clusterSyncState))
            {
                return;
            }

            if (clusterSyncState.UpdatedClusterTopology != null &&
                (!m_LastAnalyzedTopology.TryGetTarget(out var lastAnalyzedTopology) ||
                 !ReferenceEquals(lastAnalyzedTopology, clusterSyncState.UpdatedClusterTopology)))
            {
                HandleChangesInNodeAssignment(clusterSyncState);
                m_LastAnalyzedTopology.SetTarget(clusterSyncState.UpdatedClusterTopology);
            }
        }

        /// <summary>
        /// Check if the role or render node id of this node has changed.
        /// </summary>
        void HandleChangesInNodeAssignment(IClusterSyncState clusterSyncState)
        {
            // Find entry for this node
            ChangeClusterTopologyEntry? thisNodeEntry = null;
            if (clusterSyncState.UpdatedClusterTopology != null)
            {
                foreach (var entry in clusterSyncState.UpdatedClusterTopology)
                {
                    if (entry.NodeId == clusterSyncState.NodeID)
                    {
                        thisNodeEntry = entry;
                        break;
                    }
                }
            }
            if (!thisNodeEntry.HasValue)
            {
                // No entry means the node has been removed from the cluster, let's quit to avoid using power for
                // nothing...
                Quit();
                return;
            }

            clusterSyncState.NodeRole = thisNodeEntry.Value.NodeRole;
            clusterSyncState.RenderNodeID = thisNodeEntry.Value.RenderNodeId;
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RepeaterRegistered,
            MessageType.FrameData, MessageType.EmitterWaitingToStartFrame, MessageType.PropagateQuit};

        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ChangeClusterTopologyEntry>> m_LastAnalyzedTopology = new(null);
    }
}
