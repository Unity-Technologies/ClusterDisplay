using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RPCMethod : Attribute 
    {
        [AttributeUsage(AttributeTargets.Parameter)] public class FormalySerializedAsMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCExecutionStageMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCIDMarker : Attribute {}

        public readonly string formarlySerializedAs;
        public readonly RPCExecutionStage rpcExecutionStage;
        public readonly ushort rpcId;

        public RPCMethod (
            [RPCExecutionStageMarker] RPCExecutionStage rpcExecutionStage = RPCExecutionStage.ImmediatelyOnArrival, 
            [RPCIDMarker] ushort rpcId = 0,
            [FormalySerializedAsMarker] string formarlySerializedAs = "")
        {
            this.rpcExecutionStage = rpcExecutionStage;
            this.rpcId = rpcId;

            this.formarlySerializedAs = formarlySerializedAs;
        }
    }
}
