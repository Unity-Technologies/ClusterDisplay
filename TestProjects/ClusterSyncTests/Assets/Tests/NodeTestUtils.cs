using System;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// A dummy NodeState: when we require a state that does nothing.
    /// </summary>
    sealed class NullState : NodeState
    {
        public override bool ReadyToProceed => false;
        public override bool ReadyForNextFrame => false;
        public NullState() : base(null) { }
    }

    static class NodeTestUtils
    {
        public const int RxPort = 12345;
        public const int TxPort = 12346;
        public const string MulticastAddress = "224.0.1.0";
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        public const int MaxRetries = 20;

        public static readonly string AdapterName = NetworkingUtils.SelectNic().Name;

        public static readonly UDPAgent.Config udpConfig = new()
        {
            ip = MulticastAddress,
            rxPort = RxPort,
            txPort = TxPort,
            timeOut = Timeout,
            adapterName = AdapterName
        };

        public static bool RunStateUpdateUntil<T>(T state,
            Func<T, bool> pred,
            int maxRetries = MaxRetries) where T : NodeState =>
            Utilities.LoopUntil(() => pred(state) || state != state.ProcessFrame(false), maxRetries);

        public static bool RunStateLateUpdateUntil<T>(T state,
            Func<T, bool> pred,
            int maxRetries = MaxRetries) where T : NodeState =>
            Utilities.LoopUntil(() =>
            {
                var condition = pred(state);
                if (!condition)
                {
                    state.ProcessLateFrame();
                }
                return condition;
            }, maxRetries);

        public static NodeState RunStateUntilTransition(NodeState state, int maxRetries = MaxRetries)
        {
            NodeState nextState = state;
            Utilities.LoopUntil(() =>
            {
                nextState = state.ProcessFrame(false);
                return nextState != state;
            }, maxRetries);

            return nextState;
        }

        public static bool RunStateUntilReadyToProceed(NodeState state, int maxRetries = MaxRetries) =>
            RunStateUpdateUntil(state, nodeState => nodeState.ReadyToProceed, maxRetries);

        public static bool RunStateUntilReadyForNextFrame(NodeState state, int maxRetries = MaxRetries) =>
            RunStateLateUpdateUntil(state, nodeState => nodeState.ReadyForNextFrame, maxRetries);
    }
}
