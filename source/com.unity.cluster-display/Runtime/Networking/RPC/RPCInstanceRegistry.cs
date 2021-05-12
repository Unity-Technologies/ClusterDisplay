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

        /*
        if (!ObjectRegistry.TryGetInstance(out var objectRegistry))
            return false;

        var obj = objectRegistry[pipeId];
        if (obj == null)
            return false;
        */

        /*
        if (!RPCRegistry.TryGetInstance(out var rpcRegistry))
            return false;

        var rpcMethodInfo = rpcRegistry[rpcId];
        if (!rpcMethodInfo.IsValid)
            return false;
        */

        /*
        public delegate void RPCCall();
        public RPCCall[][] calls;

        private object[] parameters;

        public void PushParameters(object[] parameters)
        {
            this.parameters = parameters;
        }

        public void TryCallRPC (ushort pipeId, ushort rpcId)
        {
        }

        public void TryCallStaticRPC (ushort rpcId)
        {
        }
        */
    }

    /*
    public class RPCInterfaceRegistry<A> : RPCInterfaceRegistry
    {
    }
    */
}
