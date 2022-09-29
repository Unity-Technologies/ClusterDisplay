using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.ClusterDisplay.RPC;
using UnityEngine;

namespace Unity.ClusterDisplay.Tests
{
    public static class RPCTestUtils
    {
        public static MethodBase FindClusterRPCMethod()
        {
            var stackTrace = new StackTrace();

            int stackFrameIndex = 0;
            var stackFrame = stackTrace.GetFrame(stackFrameIndex++);

            MethodBase rpcMethod = null;
            while (stackFrame != null)
            {
                rpcMethod = stackFrame.GetMethod();
                if (rpcMethod.GetCustomAttribute<ClusterRPC>() != null)
                {
                    return rpcMethod;
                }

                stackFrame = stackTrace.GetFrame(stackFrameIndex++);
            }

            throw new System.InvalidOperationException($"Cannot find executed RPC in stack trace.");
        }
    }
}
