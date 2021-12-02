using System;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class ClusterRPC : Attribute 
    {
        // IF YOU CHANGE THE ORDER OF THESE ATTRIBUTE FIELDS, THEN YOU MUST ALSO CHANGE THE ORDER IN THE IL POST PROCESSOR.
        public readonly RPCExecutionStage rpcExecutionStage;
        public readonly string rpcHash;
        public readonly string formarlySerializedAs;

        /// <summary>
        /// Define this above the method you want to mark as an RPC.
        /// </summary>
        /// <param name="rpcExecutionStage">By default, the execution stage is Automatic. However, you can override this here.</param>
        /// <param name="rpcHash">RPC ID specification should be unsigned 16bit integer. However, in this case </param>
        /// <param name="formarlySerializedAs"></param>
        public ClusterRPC (
            RPCExecutionStage rpcExecutionStage = RPCExecutionStage.Automatic, 
            string rpcHash = "",
            string formarlySerializedAs = "")
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.formarlySerializedAs = formarlySerializedAs;
            this.rpcHash = rpcHash;
        }
    }
}
