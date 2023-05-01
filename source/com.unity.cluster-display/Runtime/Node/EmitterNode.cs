using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.ClusterDisplay.EmitterStateMachine;
using UnityEngine;
using Utils;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Information about a repeater.
    /// </summary>
    struct RepeaterStatus
    {
        /// <summary>
        /// IP Address of the repeater.
        /// </summary>
        /// <remarks>An IP of <see cref="IPAddress.None"/> is the equivalent of saying no repeater.</remarks>
        public IPAddress IP;
    }

    /// <summary>
    /// Information about repeaters.
    /// </summary>
    class RepeatersStatus
    {
        /// <summary>
        /// Get or set bits in the <see cref="NodeIdBitVector"/>.
        /// </summary>
        /// <param name="nodeId">Index of the bit to get or set (corresponding to a NodeId).</param>
        public RepeaterStatus this[byte nodeId]
        {
            get => m_Status[nodeId];
            set
            {
                m_Status[nodeId] = value;
                m_RepeaterPresence[nodeId] = (value.IP != null) && !value.IP.Equals(IPAddress.None);
            }
        }

        /// <summary>
        /// Process a <see cref="RegisteringWithEmitter"/> message updating this object if needed.
        /// </summary>
        /// <param name="registeringMessage">Received registration request message</param>
        /// <param name="acceptNew">Do we accept new repeaters?</param>
        /// <returns>Answer to be sent to the repeater.</returns>
        public RepeaterRegistered ProcessRegisteringMessage(RegisteringWithEmitter registeringMessage,
            bool acceptNew = true)
        {
            var repeaterIp = new IPAddress(registeringMessage.IPAddressBytes);
            var previousStatus = this[registeringMessage.NodeId];
            bool registrationAccepted;
            if (acceptNew)
            {
                registrationAccepted = previousStatus.IP == null ||
                    previousStatus.IP.Equals(IPAddress.None) || previousStatus.IP.Equals(repeaterIp);
                if (registrationAccepted)
                {
                    this[registeringMessage.NodeId] = new RepeaterStatus() {IP = repeaterIp};
                }
            }
            else
            {
                registrationAccepted = previousStatus.IP != null && previousStatus.IP.Equals(repeaterIp);
            }

            return new RepeaterRegistered()
            {
                NodeId = registeringMessage.NodeId,
                IPAddressBytes = registeringMessage.IPAddressBytes,
                Accepted = registrationAccepted
            };
        }

        /// <summary>
        /// Update the <see cref="RepeatersStatus"/> based on a response to the repeaters status survey.
        /// </summary>
        /// <param name="answer">Answer to the survey.</param>
        public void ProcessSurveyAnswer(RepeatersSurveyAnswer answer)
        {
            var repeaterIp = new IPAddress(answer.IPAddressBytes);
            this[answer.NodeId] = new RepeaterStatus() {IP = repeaterIp};
        }

        /// <summary>
        /// <see cref="NodeIdBitVectorReadOnly"/> representing if repeater nodes are present or not.
        /// </summary>
        public NodeIdBitVectorReadOnly RepeaterPresence => m_RepeaterPresence;

        /// <summary>
        /// Status about all possible nodes.
        /// </summary>
        RepeaterStatus[] m_Status = new RepeaterStatus[byte.MaxValue + 1];
        /// <summary>
        /// <see cref="NodeIdBitVector"/> storing if repeater nodes are present or not.
        /// </summary>
        NodeIdBitVector m_RepeaterPresence = new();
    }

    /// <summary>
    /// Configuration for an emitter node.
    /// </summary>
    struct EmitterNodeConfig
    {
        /// <summary>
        /// Number of repeaters after which the emitter will wait at startup.
        /// </summary>
        public byte ExpectedRepeaterCount { get; set; }

        /// <summary>
        /// Responses to the current state survey of every repeater (or backup) nodes in the cluster.  Specifying this
        /// collection allow skipping greeting of repeaters nodes (since the <see cref="EmitterNode"/> we are replacing
        /// already did it before us and the survey responses contains all the information we need).
        /// </summary>
        public IReadOnlyCollection<RepeatersSurveyAnswer> RepeatersSurveyResult { get; set; }
    }

    /// <summary>
    /// Class for the objects representing an emitter cluster node (a node sending its state to multiple repeater nodes).
    /// </summary>
    class EmitterNode : ClusterNode
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Node's configuration.</param>
        /// <param name="emitterConfig">Emitter specific configuration.</param>
        /// <param name="udpAgent">Object through which we will perform communication with other nodes in the cluster.
        /// Must support reception of <see cref="ReceiveMessageTypes"/>.</param>
        public EmitterNode(ClusterNodeConfig config, EmitterNodeConfig emitterConfig, IUdpAgent udpAgent)
            : base(config, udpAgent)
        {
            NodeRole = NodeRole.Emitter;
            EmitterConfig = emitterConfig;
            if (!emitterConfig.RepeatersSurveyResult?.Any() ?? true)
            {
                SetInitialState(Config.Fence is FrameSyncFence.Hardware ?
                    HardwareSyncInitState.Create(this) : new WelcomeRepeatersState(this));
            }
            else
            {
                NodeIdBitVector initialNodesUsingNetworkSync = new();
                foreach (var answer in emitterConfig.RepeatersSurveyResult)
                {
                    RepeatersStatus.ProcessSurveyAnswer(answer);
                    if (answer.StillUseNetworkSync)
                    {
                        initialNodesUsingNetworkSync[answer.NodeId] = true;
                    }
                }

                SetInitialState(new EmitFrameState(this, initialNodesUsingNetworkSync, true));
            }
        }

        /// <inheritdoc />
        public override DoFrameResult DoFrame()
        {
            // Process cluster topology changes.
            // Remarks: We need to delay that after calling the base class if there are no repeaters yet.  This will
            // happen on the first frame as this is the processing done for that first frame that discover the list of
            // repeaters.
            bool topologyChangesProcessed = false;
            if (RepeatersStatus.RepeaterPresence.SetBitsCount > 0)
            {
                if (!ProcessTopologyChanges())
                {
                    return DoFrameResult.ShouldQuit;
                }
                topologyChangesProcessed = true;
            }

            var ret = base.DoFrame();

            if (!topologyChangesProcessed && ret != DoFrameResult.ShouldQuit)
            {
                if (!ProcessTopologyChanges())
                {
                    ret = DoFrameResult.ShouldQuit;
                }
            }

            return ret;
        }

        /// <summary>
        /// Emitter specific configuration.
        /// </summary>
        public EmitterNodeConfig EmitterConfig { get; }

        /// <summary>
        /// Information about repeaters.
        /// </summary>
        public RepeatersStatus RepeatersStatus { get; } = new();

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        public static IReadOnlyCollection<MessageType> ReceiveMessageTypes => s_ReceiveMessageTypes;

        /// <summary>
        /// Process cluster topology changes
        /// </summary>
        /// <returns>Should this node continue to do its work?</returns>
        bool ProcessTopologyChanges()
        {
            if (UpdatedClusterTopology.Entries != null &&
                (!m_LastAnalyzedTopologyEntries.TryGetTarget(out var lastAnalyzedTopology) ||
                 !ReferenceEquals(lastAnalyzedTopology, UpdatedClusterTopology.Entries)))
            {
                HandleRemovedRepeaters(UpdatedClusterTopology.Entries);
                if (HandleThisNodeNotEmitterAnymore(UpdatedClusterTopology.Entries))
                {
                    return false;
                }
                m_LastAnalyzedTopologyEntries.SetTarget(UpdatedClusterTopology.Entries);
            }

            return true;
        }

        /// <summary>
        /// Check for repeaters that are not included in the cluster topology anymore.
        /// </summary>
        /// <param name="topologyEntries">List of all the entries describing the current topology of the cluster.
        /// </param>
        void HandleRemovedRepeaters(IReadOnlyList<ClusterTopologyEntry> topologyEntries)
        {
            NodeIdBitVector repeatersStillPresent = new();
            foreach (var entry in topologyEntries)
            {
                if (entry.NodeRole is NodeRole.Repeater or NodeRole.Backup)
                {
                    repeatersStillPresent[entry.NodeId] = true;
                }
            }

            foreach (var toWaitFor in RepeatersStatus.RepeaterPresence.ExtractSetBits())
            {
                if (!repeatersStillPresent[toWaitFor])
                {
                    RepeatersStatus[toWaitFor] = new ();
                }
            }
        }

        /// <summary>
        /// Validate that this node is still an emitter in the given configuration, otherwise quit...
        /// </summary>
        /// <returns>Should this node stop being an emitter?</returns>
        bool HandleThisNodeNotEmitterAnymore(IReadOnlyList<ClusterTopologyEntry> topologyEntries)
        {
            ClusterTopologyEntry? thisNodeEntry = null;
            foreach (var entry in topologyEntries)
            {
                if (entry.NodeId == Config.NodeId)
                {
                    thisNodeEntry = entry;
                    break;
                }
            }

            if (thisNodeEntry is not {NodeRole: NodeRole.Emitter})
            {
                if (thisNodeEntry is {NodeRole: not (NodeRole.Emitter or NodeRole.Unassigned)} )
                {
                    Debug.LogError($"Emitter node cannot be reconfigured to anything else, it was requested to " +
                        $"change to {thisNodeEntry.Value.NodeRole}.");
                }

                // In any case, this node cannot perform the new requested role (or lack of role), so terminate...
                Quit();
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RegisteringWithEmitter,
            MessageType.RetransmitFrameData, MessageType.RepeaterWaitingToStartFrame, MessageType.QuitReceived,
            MessageType.QuadroBarrierWarmupStatus};

        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ClusterTopologyEntry>> m_LastAnalyzedTopologyEntries = new(null);
    }
}
