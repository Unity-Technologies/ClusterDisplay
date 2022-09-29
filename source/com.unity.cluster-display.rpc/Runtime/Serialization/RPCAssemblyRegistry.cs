using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    internal static class RPCAssemblyRegistry
    {
        /// <summary>
        /// When we receive an RPC over the network, we need to identify which assembly were supposed to
        /// execute the RPC in. Foreach assembly a derrived instance of RPCInstanceRegistry is created and
        /// we use the assembly index to determine which delegate we call. See RPCInstanceRegistry's constructor.
        /// </summary>
        static readonly ushort[] m_AssemblyIndexLookUp = new ushort[ushort.MaxValue];

        internal static bool TryGetAssemblyIndex(ushort rpcId, out ushort assemblyIndex)
        {
            var storedAssemblyIndex = m_AssemblyIndexLookUp[rpcId];
            assemblyIndex = (ushort)(storedAssemblyIndex - 1);

            if (storedAssemblyIndex == 0)
                return false;

            return true;
        }

        internal static bool AssociateRPCWithAssembly (MethodInfo methodInfo, int rpcId)
        {
            if (!RPCInterfaceRegistry.TryCreateImplementationInstance(methodInfo.DeclaringType.Assembly, out var assemblyIndex))
            {
                return false;
            }

            m_AssemblyIndexLookUp[rpcId] = (ushort)(assemblyIndex + 1);
            ClusterDebug.Log($"Associated method: (RPC ID: {rpcId}, Name: {methodInfo.Name}, Type: {methodInfo.DeclaringType.Name}) with assembly: \"{methodInfo.Module.Assembly.GetName().Name}\" at index: {assemblyIndex}");
            return true;
        }
    }
}
