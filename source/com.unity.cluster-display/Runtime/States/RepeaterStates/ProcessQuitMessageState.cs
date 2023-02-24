using System;

namespace Unity.ClusterDisplay.RepeaterStateMachine
{
    /// <summary>
    /// <see cref="NodeState"/> that process a <see cref="MessageType.PropagateQuit"/>.
    /// </summary>
    class ProcessQuitMessageState: NodeState<RepeaterNode>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        public ProcessQuitMessageState(RepeaterNode node)
            : base(node) { }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            Node.UdpAgent.SendMessage(MessageType.QuitReceived, new QuitReceived()
                {NodeId = Node.Config.NodeId});
            Node.Quit();
            return (null, DoFrameResult.FrameDone);
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(RepeatFrameState));
    }
}
