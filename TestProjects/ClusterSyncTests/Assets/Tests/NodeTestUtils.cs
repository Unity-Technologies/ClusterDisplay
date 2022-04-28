using System;

namespace Unity.ClusterDisplay.Tests
{
    /// <summary>
    /// A dummy NodeState: when we require a state that does nothing.
    /// </summary>
    sealed class NullState : NodeState
    {
        public NullState() : base(null) { }
    }

    static class NodeTestUtils
    {
        public const int rxPort = 12345;
        public const int txPort = 12346;
        public const string multicastAddress = "224.0.1.0";
        public const int timeoutSeconds = 10;
        public const int maxRetries = 20;

        public static readonly string adapterName = NetworkingUtils.SelectNic().Name;

        public static readonly UDPAgent.Config udpConfig = new()
        {
            ip = multicastAddress,
            rxPort = rxPort,
            txPort = txPort,
            timeOut = timeoutSeconds * 1000,
            adapterName = adapterName
        };

        public static bool RunStateUpdateUntil<T>(T state,
            Func<T, bool> pred,
            int maxRetries = maxRetries) where T : NodeState =>
            Utilities.LoopUntil(() => pred(state) || state != state.ProcessFrame(false), maxRetries);

        public static bool RunStateLateUpdateUntil<T>(T state,
            Func<T, bool> pred,
            int maxRetries = maxRetries) where T : NodeState =>
            Utilities.LoopUntil(() =>
            {
                var condition = pred(state);
                if (!condition)
                {
                    state.ProcessLateFrame();
                }
                return condition;
            }, maxRetries);

        public static NodeState RunStateUntilTransition(NodeState state, int maxRetries = maxRetries)
        {
            NodeState nextState = state;
            Utilities.LoopUntil(() =>
            {
                nextState = state.ProcessFrame(false);
                return nextState != state;
            }, maxRetries);

            return nextState;
        }

        public static bool RunStateUntilReadyToProceed(NodeState state, int maxRetries = maxRetries) =>
            RunStateUpdateUntil(state, nodeState => nodeState.ReadyToProceed, maxRetries);

        public static bool RunStateUntilReadyForNextFrame(NodeState state, int maxRetries = maxRetries) =>
            RunStateLateUpdateUntil(state, nodeState => nodeState.ReadyForNextFrame, maxRetries);
    }
}
