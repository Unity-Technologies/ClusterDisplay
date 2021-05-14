using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.ClusterDisplay;
using Unity.Collections;

namespace Unity.ClusterDisplay
{
    public abstract class RPCInterfaceRegistry : SingletonScriptableObject<RPCInterfaceRegistry>
    {
        protected abstract bool OnTryCall(ushort pipeId, ushort rpcId, ref int startPos);
        public static bool TryCall (ushort pipeId, ushort rpcId, ref int startPos)
        {
            if (!TryGetInstance(out var instanceRegistry))
                return false;
            return instanceRegistry.OnTryCall(pipeId, rpcId, ref startPos);
        }
    }
}
