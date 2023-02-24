using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.RepeaterStateMachine;
using UnityEngine;

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
            UdpAgent.AddPreProcess(UdpAgentPreProcessPriorityTable.EmitterPlaceholder, PreProcessReceivedMessage);
            SetInitialState(Config.Fence is FrameSyncFence.Hardware ?
                HardwareSyncInitState.Create(this) : new RegisterWithEmitterState(this));
        }

        /// <summary>
        /// Index of the last frame we received the data of.
        /// </summary>
        public ulong LastReceivedFrameIndex { get; set; }

        /// <inheritdoc />
        public override DoFrameResult DoFrame()
        {
            if (!ProcessTopologyChanges())
            {
                return DoFrameResult.ShouldQuit;
            }
            return base.DoFrame();
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        /// <summary>
        /// Is a switch from the role of backup to emitter ready to be done?
        /// </summary>
        public virtual bool IsBackupToEmitterSwitchReady()
        {
            if (m_IsBackupToEmitterSwitchReady)
            {
                return true;
            }

            ProcessTopologyChanges();
            if (m_EmitterPlaceholder is {RepeatersSynchronized: true} &&
                m_EmitterPlaceholder.SurveyStableSince > TimeSpan.FromMilliseconds(500))
            {
                m_IsBackupToEmitterSwitchReady = true;
                m_RepeatersSurveyResult = m_EmitterPlaceholder.RepeatersSurveyResult;
                m_EmitterPlaceholder.Dispose();
                m_EmitterPlaceholder = null;
                (m_EmitterPlaceholderUdpAgent as IDisposable)?.Dispose();
                m_EmitterPlaceholderUdpAgent = null;
            }

            return m_IsBackupToEmitterSwitchReady;
        }

        /// <summary>
        /// <see cref="RepeatersSurveyAnswer"/> of every repeater (or backup) nodes once
        /// <see cref="IsBackupToEmitterSwitchReady"/> returns true.
        /// </summary>
        /// <remarks>Will always be empty until IsBackupToEmitterSwitchReady returns true at least once.</remarks>
        public IReadOnlyCollection<RepeatersSurveyAnswer> RepeatersSurveyResult => m_RepeatersSurveyResult;

        /// <summary>
        /// Process cluster topology changes
        /// </summary>
        /// <remarks>Should this node continue to do its work?</remarks>
        bool ProcessTopologyChanges()
        {
            if (UpdatedClusterTopology.Entries != null &&
                (!m_LastAnalyzedTopology.TryGetTarget(out var lastAnalyzedTopology) ||
                    !ReferenceEquals(lastAnalyzedTopology, UpdatedClusterTopology.Entries)))
            {
                if (!HandleChangesInNodeAssignment(UpdatedClusterTopology.Entries))
                {
                    return false;
                }
                m_LastAnalyzedTopology.SetTarget(UpdatedClusterTopology.Entries);
            }

            return true;
        }

        /// <summary>
        /// Check if the role or render node id of this node has changed.
        /// </summary>
        /// <remarks>Should this node continue to do its work?</remarks>
        bool HandleChangesInNodeAssignment(IReadOnlyList<ClusterTopologyEntry> entries)
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
                return false;
            }

            if (NodeRole == NodeRole.Backup && thisNodeEntry.Value.NodeRole == NodeRole.Emitter)
            {
                if (m_EmitterPlaceholder == null)
                {
                    m_EmitterPlaceholderUdpAgent = UdpAgent.Clone(EmitterPlaceholder.ReceiveMessageTypes.ToArray());
                    m_EmitterPlaceholder = new(UpdatedClusterTopology, m_EmitterPlaceholderUdpAgent);
                }
            }
            else
            {
                NodeRole = thisNodeEntry.Value.NodeRole;
                RenderNodeId = thisNodeEntry.Value.RenderNodeId;
            }

            return true;
        }

        /// <summary>
        /// Preprocess a received message to detect request for survey (and to answer them as a good citizen).
        /// </summary>
        /// <param name="received">Received <see cref="ReceivedMessageBase"/> to preprocess.</param>
        /// <returns>What to do of the received message.</returns>
        PreProcessResult PreProcessReceivedMessage(ReceivedMessageBase received)
        {
            // Answer repeaters surveys.
            if (received.Type != MessageType.SurveyRepeaters)
            {
                return PreProcessResult.PassThrough();
            }

            UdpAgent.SendMessage(MessageType.RepeatersSurveyAnswer, new RepeatersSurveyAnswer() {
                NodeId = Config.NodeId,
                IPAddressBytes = BitConverter.ToUInt32(UdpAgent.AdapterAddress.GetAddressBytes()),
                LastReceivedFrameIndex = LastReceivedFrameIndex,
                StillUseNetworkSync = UsingNetworkSync
            });
            return PreProcessResult.Stop();
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RepeaterRegistered,
            MessageType.FrameData, MessageType.EmitterWaitingToStartFrame, MessageType.PropagateQuit,
            MessageType.QuadroBarrierWarmupHeartbeat, MessageType.SurveyRepeaters, 
            MessageType.RetransmitReceivedFrameData
        };

        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ClusterTopologyEntry>> m_LastAnalyzedTopology = new(null);

        /// <summary>
        /// Object created when a change from <see cref="NodeRole.Backup"/> to <see cref="NodeRole.Emitter"/> is
        /// detected to prepare the terrain before doing the change.
        /// </summary>
        EmitterPlaceholder m_EmitterPlaceholder;
        /// <summary>
        /// <see cref="IUdpAgent"/> used by <see cref="m_EmitterPlaceholder"/>.
        /// </summary>
        IUdpAgent m_EmitterPlaceholderUdpAgent;

        /// <summary>
        /// Set to true once we had a <c>m_EmitterPlaceholder.IsBackupToEmitterSwitchReady == true</c>.
        /// </summary>
        bool m_IsBackupToEmitterSwitchReady;
        /// <summary>
        /// <c>m_EmitterPlaceholder.RepeatersSurveyResult</c> once
        /// <c>m_EmitterPlaceholder.IsBackupToEmitterSwitchReady == true</c>.
        /// </summary>
        IReadOnlyCollection<RepeatersSurveyAnswer> m_RepeatersSurveyResult = new RepeatersSurveyAnswer[]{};
    }
}
