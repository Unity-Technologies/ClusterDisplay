using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public static class WrapperGenerator
    {
        public static bool TryGenerateWrapper (ref RPCMethodInfo rpcMethodInfo)
        {
            var rpcDeclaringType = rpcMethodInfo.methodInfo.DeclaringType;
            var rpcParameters = rpcMethodInfo.methodInfo.GetParameters();
            var rpcReturnType = rpcMethodInfo.methodInfo.ReturnParameter;

            return false;
        }
    }
}
