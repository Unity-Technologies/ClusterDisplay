using System.Linq;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.ClusterDisplay.RPC
{
    public struct RPCMethodInfo
    {
        public readonly MethodInfo methodInfo;
        public readonly ushort rpcId;

        public RPCExecutionStage rpcExecutionStage;
        public readonly bool usingWrapper;

        public bool IsValid => methodInfo != null;
        public bool IsStatic => methodInfo != null ? methodInfo.IsStatic : false;

        public RPCMethodInfo (
            ushort rpcId, 
            RPCExecutionStage rpcExecutionStage, 
            MethodInfo methodInfo, 
            bool usingWrapper)
        {
            this.rpcId = rpcId;
            this.rpcExecutionStage = rpcExecutionStage;
            this.methodInfo = methodInfo;
            this.usingWrapper = usingWrapper;
        }
    }
}
