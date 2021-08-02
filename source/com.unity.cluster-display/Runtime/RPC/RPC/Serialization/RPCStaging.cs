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

            var methodUniqueID = MethodToUniqueId(methodInfo);
            if (rpcRegistry.stagedMethods.TryGetValue(methodUniqueID, out var serializedMethod))
                return;

            if (!ReflectionUtils.DetermineIfMethodIsRPCCompatible(methodInfo))
            {
                Debug.LogError($"Unable to register method: \"{methodInfo.Name}\" declared in type: \"{methodInfo.DeclaringType}\", one or more of the method's parameters is not a value type or one of the parameter's members is not a value type.");
                return;
            }

            serializedMethod = SerializedMethod.Create(methodInfo);
            rpcRegistry.stagedMethods.Add(methodUniqueID, serializedMethod);
        }
    }
}
