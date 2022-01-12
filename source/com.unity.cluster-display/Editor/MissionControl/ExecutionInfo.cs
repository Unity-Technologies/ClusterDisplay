namespace Unity.ClusterDisplay.MissionControl
{
    readonly struct ExecutionInfo
    {
        readonly int NodeID;
        readonly int HandshakeTimeoutMilliseconds;
        readonly int TimeoutMilliseconds;
    }
}
