using System;
using Unity.ClusterDisplay.EmitterStateMachine;
using Unity.ClusterDisplay.RepeaterStateMachine;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Hardware synchronization state.
    /// </summary>
    /// <remarks>
    /// This state should be executed before the handshake.
    /// <see cref="HardwareSyncInitState"/> is a "null" state (does nothing).
    /// Derive from this class and return an instance of the child in
    /// <see cref="Create"/> to perform actual initialization of hardware.
    /// </remarks>
    class HardwareSyncInitState: NodeState
    {
        public static NodeState Create(ClusterNode node)
        {
#if UNITY_STANDALONE_WIN
            return node.NodeRole switch
            {
                NodeRole.Emitter => new QuadroSyncInitEmitterState(node),
                _ => new QuadroSyncInitRepeaterState(node)
            };
#else
            ClusterDebug.LogWarning("Hardware synchronization is not available in this environment");
            return new HardwareSyncInitState(node);
#endif
        }

        protected HardwareSyncInitState(ClusterNode node)
            : base(node)
        {
        }

        protected override (NodeState, DoFrameResult?) DoFrameImplementation()
        {
            NodeState nextState = Node switch {
                EmitterNode emitterNode => new WelcomeRepeatersState(emitterNode),
                RepeaterNode repeaterNode => new RegisterWithEmitterState(repeaterNode),
                _ => throw new ArgumentOutOfRangeException(nameof(Node))
            };
            return (nextState, null);
        }

        protected override IntPtr GetProfilerMarker() => s_ProfilerMarker;

        /// <summary>
        /// Value returned by <see cref="GetProfilerMarker"/>.
        /// </summary>
        static IntPtr s_ProfilerMarker = CreateProfilingMarker(nameof(HardwareSyncInitState));
    }
}
