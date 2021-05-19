using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class RPCMethod : System.Attribute 
    {
        private RPCExecutionStage rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival;
        public RPCMethod (RPCExecutionStage rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival)
        {
            this.rpcExecutionStage = rpcExecutionStage;
        }
    }
}
