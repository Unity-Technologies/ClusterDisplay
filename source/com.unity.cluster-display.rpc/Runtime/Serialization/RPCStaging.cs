using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    public partial class RPCRegistry
    {
        private Dictionary<string, SerializedMethod> stagedMethods = new Dictionary<string, SerializedMethod>();

        public static void StageRPCToRegister (MethodInfo methodInfo)
        {
            if (!TryGetInstance(out var rpcRegistry))
                return;

            var rpcHash = MethodInfoToRPCHash(methodInfo);
            if (rpcRegistry.stagedMethods.TryGetValue(rpcHash, out var serializedMethod))
                return;

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                ClusterDebug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                return;
            }

            serializedMethod = SerializedMethod.Create(methodInfo);
            rpcRegistry.stagedMethods.Add(rpcHash, serializedMethod);
        }
    }
}
