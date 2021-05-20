using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RPCMethod : Attribute 
    {
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCExecutionStageMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCIDMarker : Attribute {}

        public readonly RPCExecutionStage rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival;
        public readonly ushort rpcId = 0;

        public RPCMethod (
            [RPCExecutionStageMarker] RPCExecutionStage rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival, 
            [RPCIDMarker] ushort rpcId = 0)
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.rpcId = rpcId;
        }
    }
}
