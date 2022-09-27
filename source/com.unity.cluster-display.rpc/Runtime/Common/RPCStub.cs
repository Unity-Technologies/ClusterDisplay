using System.Runtime.InteropServices;


namespace Unity.ClusterDisplay.RPC
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RPCStub
    {
        public bool isStatic;
        public bool overrideRPCExecutionStage;
        public int rpcExecutionStage;
        public RPMethodStub methodStub;

        public static RPCStub Create (ref RPCMethodInfo rpcMethodInfo)
        {
            return new RPCStub
            {
                isStatic = rpcMethodInfo.IsStatic,
                overrideRPCExecutionStage = rpcMethodInfo.overrideRPCExecutionStage,
                rpcExecutionStage = (int)rpcMethodInfo.rpcExecutionStage,
                methodStub = RPMethodStub.Create(rpcMethodInfo.methodInfo)
            };
        }
    }
}
