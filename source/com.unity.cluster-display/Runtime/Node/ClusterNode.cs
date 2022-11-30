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
        /// Object to use to perform network access (sending or receiving).
        /// </summary>
        public IUdpAgent UdpAgent { get; private set;  }

        /// <summary>
        /// Index of the frame we are currently dealing with.
        /// </summary>
        /// <remarks>Every "physical node" start with a FrameIndex of 0 and it increase in lock step between all the
        /// nodes as the time pass.  However, the <see cref="ClusterNode"/> might start at a different FrameIndex
        /// when the role of a node changes during the process lifetime (eg. when a <see cref="EmitterNode"/> replaces
        /// a <see cref="BackupNode"/>).</remarks>
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
        public void DoFrame()
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
            builder.AppendFormat("\tNode ID: {0}", Config.NodeId);
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

    /// <summary>
    /// Not yet implemented, TODO as we start working on redundancy.  Mostly kept so that comments can reference it :)
    /// </summary>
    class BackupNode : ClusterNode
    {
        BackupNode()
            : base(new ClusterNodeConfig(), null)
        {
            throw new NotImplementedException();
        }
    }
}
