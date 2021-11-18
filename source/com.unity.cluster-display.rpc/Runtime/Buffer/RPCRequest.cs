using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC
{
    internal struct RPCRequest
    {
        public ushort rpcId;
        public RPCExecutionStage rpcExecutionStage;

        public ushort pipeId;
        public bool isStaticRPC;

        public buint parametersPayloadSize;
        public ushort assemblyIndex;
    }
}
