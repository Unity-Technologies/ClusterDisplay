using System;
using System.Text;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Configuration for a cluster node.
    /// </summary>
    struct ClusterNodeConfig
    {
        /// <summary>
        /// Identifier uniquely identifying the node throughout the cluster.
        /// </summary>
        public byte NodeId { get; set; }
        /// <summary>
        /// Time to establish communication between emitter and repeaters.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; set; }
        /// <summary>
        /// Timeout for any other communication than initial handshake.  An emitter waiting for repeaters longer than
        /// this is allowed to kick him out.  A repeater not getting news from the emitter for longer than this is
        /// allowed to abort.
        /// </summary>
        public TimeSpan CommunicationTimeout { get; set; }
        /// <summary>
        /// Are the repeaters delayed by one frame.
        /// </summary>
        public bool RepeatersDelayed { get; set; }
        /// <summary>
        /// Synchronization strategy
        /// </summary>
        public FrameSyncFence Fence { get; set; }
        /// <summary>
        /// The input subsystem synchronized by the cluster.
        /// </summary>
        /// <remarks>
        /// You should select the one that corresponds to your project setup. If your project does not handle
        /// inputs, use <see cref="ClusterDisplay.InputSync.None"/> to minimize network traffic.
        /// </remarks>
        public InputSync InputSync { get; set; }
    }

    /// <summary>
    /// Base class for the classes of objects representing the cluster node.
    /// </summary>
    class ClusterNode: IDisposable
    {
        /// <summary>
        /// Configuration of the node.
        /// </summary>
        /// <remarks>Constant throughout the lifetime of the node.</remarks>
        public ClusterNodeConfig Config { get; }

        /// <summary>
        /// Timeout to use for the different network communication after the initial handshake.
        /// </summary>
        /// <remarks>However, the initial handshake might a little bit more involved than actually shaking hands...
        /// Repeaters will shake hands with the emitter and then move on on their first frame, but the emitter might
        /// still be shaking hands with other repeaters that are a little bit longer to join the party.  So use the
        /// handshake timeout for the first two frame and then move on to the normal communication timeout.</remarks>
        public TimeSpan EffectiveCommunicationTimeout =>
            FrameIndex > 1 ? Config.CommunicationTimeout : Config.HandshakeTimeout;

        /// <summary>
        /// Object to use to perform network access (sending or receiving).
        /// </summary>
        public IUdpAgent UdpAgent { get; private set;  }

        /// <summary>
        /// Index of the frame we are currently dealing with.
        /// </summary>
        /// <remarks>Every "physical node" start with a FrameIndex of 0 and it increase in lock step between all the
        /// nodes as the time pass.</remarks>
        public ulong FrameIndex { get; private set; }

        /// <summary>
        /// Gets whether the nodes are synchronized at the beginning of the frame with a network fence.
        /// If this property is <c>false</c>, it means that the node is relying some other synchronization
        /// mechanism (likely hardware).
        /// </summary>
        /// <remarks>
        /// Emitter node: Will always use network fence (<see cref="RepeaterWaitingToStartFrame"/> and
        /// <see cref="EmitterWaitingToStartFrame"/>) for as long as some of the repeaters indicate they are using
        /// network fence.  This property is more for an informative purpose.<br/><br/>
        /// Repeater node: If <c>false</c> it can set <see cref="RepeaterWaitingToStartFrame.WillUseNetworkSyncOnNextFrame"/>
        /// to false and start relying exclusively on external synchronization.
        /// <br/><br/>
        /// Note: Initializing the node with <see cref="FrameSyncFence.External"/> will cause this property to be
        /// always <c>false</c>.
        /// </remarks>
        public bool UsingNetworkSync { get; internal set; } = true;

        /// <summary>
        /// Method called to perform the work the node has to do for the current frame
        /// </summary>
        public virtual void DoFrame()
        {
            NodeState newState;
            do
            {
                newState = m_CurrentState.DoFrame();
                if (newState != null)
                {
                    var disposableState = m_CurrentState as IDisposable;
                    disposableState?.Dispose();
                    m_CurrentState = newState;
                }
            } while (newState != null);
        }

        /// <summary>
        /// Method called at the end of each frame preparing for next frame
        /// </summary>
        public void ConcludeFrame()
        {
            ++FrameIndex;
        }

        public virtual string GetDebugString(NetworkStatistics networkStatistics)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("\tNode (Role / ID / Render ID): {0} / {1} / {2}", NodeRole, Config.NodeId,
                RenderNodeId);
            builder.AppendLine();
            builder.AppendFormat("\tFrame: {0}", FrameIndex);
            builder.AppendLine();

            var snapshot = networkStatistics.ComputeSnapshot();
            builder.AppendFormat("\tNetwork stats (over the last {0:0.00} seconds, received / sent / sent repeat): ",
                snapshot.Interval.TotalSeconds);
            builder.AppendLine();

            foreach (var (type, typeStats) in snapshot.TypeStatistics)
            {
                builder.AppendFormat("\t\t{0}: {1} / {2}", type, typeStats.Received, typeStats.Sent);
                // Any sent RetransmitFrameData is obviously to get a repeat, so skip it (and repeat of
                // RetransmitFrameData are also not tracked).
                if (type != MessageType.RetransmitFrameData)
                {
                    builder.AppendFormat(" / {0}", typeStats.SentRepeat);
                }
                builder.AppendLine();
            }

            if (snapshot.RetransmitFrameDataSequence + snapshot.RetransmitFrameDataIdle > 0)
            {
                builder.AppendLine("\t\tRetransmitFrameData reasons:");
                builder.AppendFormat("\t\t\tSequence: {0}", snapshot.RetransmitFrameDataSequence);
                builder.AppendLine();
                builder.AppendFormat("\t\t\tIdle: {0}", snapshot.RetransmitFrameDataIdle);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>
        /// Role of the node in the cluster.
        /// </summary>
        public NodeRole NodeRole { get; protected set; }

        /// <summary>
        /// Adjusted node ID for rendering.
        /// </summary>
        /// <remarks>
        /// This could differ from NodeID if not all nodes perform rendering (non-rendering nodes are skipped).
        /// </remarks>
        public byte RenderNodeId { get; set; }

        /// <summary>
        /// Updated topology of the cluster (when updated while running).
        /// </summary>
        public ClusterTopology UpdatedClusterTopology { get; } = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Node's configuration.</param>
        /// <param name="udpAgent">Object through which we will perform communication with other nodes in the cluster.
        /// </param>
        protected ClusterNode(ClusterNodeConfig config, IUdpAgent udpAgent)
        {
            Config = config;
            UdpAgent = udpAgent;
            if (config.Fence is FrameSyncFence.External)
            {
                UsingNetworkSync = false;
                ClusterDebug.Log("Using external sync. Bypassing network fence");
            }
        }

        /// <summary>
        /// Set the initial state of the node.
        /// </summary>
        /// <param name="initialState">Node's initial <see cref="NodeState"/>.</param>
        protected void SetInitialState(NodeState initialState)
        {
            m_CurrentState = initialState;
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            var disposableUdpAgent = UdpAgent as IDisposable;
            disposableUdpAgent?.Dispose();
            var disposableState = m_CurrentState as IDisposable;
            disposableState?.Dispose();
        }

        /// <summary>
        /// Current state of the node.
        /// </summary>
        NodeState m_CurrentState;
    }
}
