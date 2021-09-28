using System;

namespace Unity.ClusterDisplay.RPC
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
        public readonly int rpcId;

        private int ValidateRPCId (int id)
        {
            var clampedId = id;
            if (clampedId < -1)
                clampedId = -1;
            else if (clampedId > (int)Constants.MaxRPCID)
                clampedId = (int)Constants.MaxRPCID;
            
            #if UNITY_EDITOR
            if ((ulong)rpcId >= Constants.MaxRPCID)
                UnityEngine.Debug.LogError($"You should not be overriding the RPC ID with: ({rpcId}) when it's larger then the max ID ({Constants.MaxRPCID}).");
            #endif
            
            return clampedId;
        }

        /// <summary>
        /// Define this above the method you want to mark as an RPC.
        /// </summary>
        /// <param name="rpcExecutionStage">By default, the execution stage is Automatic. However, you can override this here.</param>
        /// <param name="rpcId">RPC ID specification should be unsigned 16bit integer. However, in this case </param>
        /// <param name="formarlySerializedAs"></param>
        public ClusterRPC (
            [RPCExecutionStageMarker] RPCExecutionStage rpcExecutionStage = RPCExecutionStage.Automatic, 
            [RPCIDMarker] int rpcId = -1,
            [FormalySerializedAsMarker] string formarlySerializedAs = "")
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.formarlySerializedAs = formarlySerializedAs;
            this.rpcId = ValidateRPCId(rpcId);
        }
    }
}
