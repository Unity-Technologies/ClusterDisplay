using System;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    [Obsolete("RPCMethod has been renamed to ClusterRPC")]
    public class RPCMethod : ClusterRPC {}

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    [Obsolete("RPC has been renamed to ClusterRPC")]
    public class RPC : ClusterRPC {}

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ClusterRPC : Attribute 
    {
        [AttributeUsage(AttributeTargets.Parameter)] public class FormalySerializedAsMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCExecutionStageMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCIDMarker : Attribute {}

        public readonly string formarlySerializedAs;
        public readonly RPCExecutionStage rpcExecutionStage;
        public readonly ushort rpcId;

        public ClusterRPC (
            [RPCExecutionStageMarker] RPCExecutionStage rpcExecutionStage = RPCExecutionStage.Automatic, 
            [RPCIDMarker] ushort rpcId = 0,
            [FormalySerializedAsMarker] string formarlySerializedAs = "")
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.rpcId = rpcId;

            this.formarlySerializedAs = formarlySerializedAs;
        }
    }
}
