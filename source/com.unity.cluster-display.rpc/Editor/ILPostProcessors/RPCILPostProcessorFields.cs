using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private SerializedRPC[] cachedSerializedRPCS = null;

        private readonly static Dictionary<string, RPCILGenerator> cachedOnTryCallProcessors = new Dictionary<string, RPCILGenerator>();
        private readonly static Dictionary<string, RPCILGenerator> cachedOnTryStaticCallProcessors = new Dictionary<string, RPCILGenerator>();
        private readonly static Dictionary<string, QueuedRPCILGenerator> cachedQueuedRPCILGenerators = new Dictionary<string, QueuedRPCILGenerator>();

        private static bool TryGetCachedGetIsEmitterMarkerMethod (out MethodInfo getIsEmitterMethod)
        {
            if (!CecilUtils.TryFindPropertyGetMethodWithAttribute<ClusterDisplayState.IsEmitterMarker>(typeof(ClusterDisplayState), out getIsEmitterMethod))
            {
                getIsEmitterMethod = null;
                return false;
            }

            return true;
        }

        private static bool TryGetGetInstanceMethodRef (ModuleDefinition moduleDef, out MethodReference getInstanceMethodRef)
        {
            if (!CecilUtils.TryFindMethodWithAttribute<SceneObjectsRegistry.GetInstanceMarker>(typeof(SceneObjectsRegistry), out var methodInfo))
            {
                getInstanceMethodRef = null;
                return false;
            }

            if (!CecilUtils.TryImport(moduleDef, methodInfo, out var methodRef))
            {
                getInstanceMethodRef = null;
                return false;
            }

            getInstanceMethodRef = methodRef;
            return true;
        }

        private static bool TryGetCachedQueuedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out QueuedRPCILGenerator queuedRPCILGenerator)
        {
            if (cachedQueuedRPCILGenerators.TryGetValue(compiledAssemblyDef.FullName, out queuedRPCILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            queuedRPCILGenerator = new QueuedRPCILGenerator(cachedGeneratedRPCILTypeRef);
            if (!queuedRPCILGenerator.TrySetup())
                return false;

            return true;
        }

        private static bool TryGetCachedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            if (cachedOnTryCallProcessors.TryGetValue(compiledAssemblyDef.FullName, out rpcILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            rpcILGenerator = new RPCILGenerator(cachedGeneratedRPCILTypeRef);
            if (!rpcILGenerator.TrySetup(typeof(RPCInterfaceRegistry.OnTryCallMarker)))
                return false;

            return true;
        }

        private static bool TryGetCachedStaticRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            if (cachedOnTryStaticCallProcessors.TryGetValue(compiledAssemblyDef.FullName, out rpcILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            rpcILGenerator = new RPCILGenerator(cachedGeneratedRPCILTypeRef);
            if (!rpcILGenerator.TrySetup(typeof(RPCInterfaceRegistry.OnTryStaticCallMarker)))
                return false;

            return true;
        }

        private static void FlushCache ()
        {
            cachedOnTryCallProcessors.Clear();
            cachedOnTryStaticCallProcessors.Clear();
            cachedQueuedRPCILGenerators.Clear();
        }
    }
}
