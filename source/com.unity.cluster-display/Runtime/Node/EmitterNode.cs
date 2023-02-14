using System;
using System.Collections.Generic;
using System.Net;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.ClusterDisplay.MissionControl.Capsule;
using Unity.ClusterDisplay.Utils;
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
            EmitterConfig = emitterConfig;
            SetInitialState(Config.Fence is FrameSyncFence.Hardware ?
                HardwareSyncInitState.Create(this) : new WelcomeRepeatersState(this));
        }

        /// <inheritdoc />
        public override void DoFrame()
        {
            // Process cluster topology changes.
            // Remarks: We need to delay that after calling the base class if there are no repeaters yet.  This will
            // happen on the first frame as this is the processing done for that first frame that discover the list of
            // repeaters.
            bool topologyChangesProcess = false;
            if (RepeatersStatus.RepeaterPresence.SetBitsCount > 0)
            {
                ProcessTopologyChanges();
                topologyChangesProcess = true;
            }

            base.DoFrame();

            if (!topologyChangesProcess)
            {
                ProcessTopologyChanges();
            }
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
                HandleRemovedRepeaters(clusterSyncState.UpdatedClusterTopology);
                m_LastAnalyzedTopology.SetTarget(clusterSyncState.UpdatedClusterTopology);
            }
        }

        /// <summary>
        /// Check for repeaters that are not included in the cluster topology anymore.
        /// </summary>
        /// <param name="topologyEntries">List of all the entries describing the current topology of the cluster.
        /// </param>
        void HandleRemovedRepeaters(IReadOnlyList<ChangeClusterTopologyEntry> topologyEntries)
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
        /// <see cref="MessageType"/> that the UdpClient must process upon reception.
        /// </summary>
        static MessageType[] s_ReceiveMessageTypes = {MessageType.RegisteringWithEmitter,
            MessageType.RetransmitFrameData, MessageType.RepeaterWaitingToStartFrame, MessageType.QuitReceived};

        /// <summary>
        /// Last analyzed topology change.
        /// </summary>
        WeakReference<IReadOnlyList<ChangeClusterTopologyEntry>> m_LastAnalyzedTopology = new(null);
    }
}
