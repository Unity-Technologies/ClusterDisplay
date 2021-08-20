using UnityEngine;


namespace Unity.ClusterDisplay.RPC
{
    [System.Serializable]
    public struct SerializedRPC
    {
        [SerializeField] public ushort rpcId;
        [SerializeField] public bool isStatic;
        [SerializeField] public bool overrideRPCExecutionStage;
        [SerializeField] public int rpcExecutionStage;
        [SerializeField] public SerializedMethod method;

        public static SerializedRPC Create (ref RPCMethodInfo rpcMethodInfo)
        {
            return new SerializedRPC
            {
                rpcId = rpcMethodInfo.rpcId,
                isStatic = rpcMethodInfo.IsStatic,
                overrideRPCExecutionStage = rpcMethodInfo.overrideRPCExecutionStage,
                rpcExecutionStage = (int)rpcMethodInfo.rpcExecutionStage,
                method = SerializedMethod.Create(rpcMethodInfo.methodInfo)
            };
        }
    }

    [System.Serializable]
    public struct RPCStubs
    {
        public SerializedRPC[] rpcs;
        public SerializedMethod[] stagedMethods;
    }
}
