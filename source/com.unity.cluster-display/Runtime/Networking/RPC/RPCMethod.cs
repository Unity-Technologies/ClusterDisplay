﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    [Obsolete("RPCMethod has been renamed to RPC")]
    public class RPCMethod : RPC {}

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class RPC : Attribute 
    {
        [AttributeUsage(AttributeTargets.Parameter)] public class FormalySerializedAsMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCExecutionStageMarker : Attribute {}
        [AttributeUsage(AttributeTargets.Parameter)] public class RPCIDMarker : Attribute {}

        public readonly string formarlySerializedAs;
        public readonly RPCExecutionStage rpcExecutionStage;
        public readonly ushort rpcId;

        public RPC (
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
