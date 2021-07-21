using System.Reflection;
using Mono.Cecil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private SerializedRPC[] cachedSerializedRPCS = null;

        private static TypeReference cachedGeneratedRPCILTypeRef;
        private static MethodInfo cachedGetIsMasterMethod;
        private static MethodReference cachedGetInstanceMethodRef;

        private static RPCILGenerator cachedOnTryCallProcessor;
        private static RPCILGenerator cachedOnTryStaticCallProcessor;
        private static QueuedRPCILGenerator cachedQueuedRPCILGenerator;

        private static bool TryGetCachedGetIsMasterMarkerMethod (out MethodInfo getIsMasterMethod)
        {
            if (cachedGetIsMasterMethod == null && !CecilUtils.TryFindPropertyGetMethodWithAttribute<ClusterDisplayState.IsMasterMarker>(typeof(ClusterDisplayState), out cachedGetIsMasterMethod))
            {
                getIsMasterMethod = null;
                return false;
            }

            getIsMasterMethod = cachedGetIsMasterMethod;
            return true;
        }

        private static bool TryGetGetInstanceMethodRef (ModuleDefinition moduleDef, out MethodReference getInstanceMethodRef)
        {
            if (cachedGetInstanceMethodRef != null)
            {
                getInstanceMethodRef = cachedGetInstanceMethodRef;
                return true;
            }

            if (!CecilUtils.TryFindMethodWithAttribute<SceneObjectsRegistry.GetInstanceMarker>(typeof(SceneObjectsRegistry), out var methodInfo))
            {
                getInstanceMethodRef = null;
                return false;
            }

            return (getInstanceMethodRef = cachedGetInstanceMethodRef = moduleDef.ImportReference(methodInfo)) != null;
        }

        private static bool TryGetCachedQueuedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out QueuedRPCILGenerator queuedRPCILGenerator)
        {
            queuedRPCILGenerator = null;
            if (cachedQueuedRPCILGenerator == null)
            {
                if (cachedGeneratedRPCILTypeRef == null)
                {
                    if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                        return false;
                }

                cachedQueuedRPCILGenerator = new QueuedRPCILGenerator(cachedGeneratedRPCILTypeRef);
                if (!cachedQueuedRPCILGenerator.TrySetup())
                    return false;
            }

            return (queuedRPCILGenerator = cachedQueuedRPCILGenerator) != null;
        }

        private static bool TryGetCachedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            rpcILGenerator = null;
            if (cachedOnTryCallProcessor == null)
            {
                if (cachedGeneratedRPCILTypeRef == null)
                {
                    if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                        return false;
                }

                cachedOnTryCallProcessor = new RPCILGenerator(cachedGeneratedRPCILTypeRef);
                if (!cachedOnTryCallProcessor.TrySetup(typeof(RPCInterfaceRegistry.OnTryCallMarker)))
                    return false;
            }

            return (rpcILGenerator = cachedOnTryCallProcessor) != null;
        }

        private static bool TryGetCachedStaticRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            rpcILGenerator = null;
            if (cachedOnTryStaticCallProcessor == null)
            {
                if (cachedGeneratedRPCILTypeRef == null)
                {
                    if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                        return false;
                }

                cachedOnTryStaticCallProcessor = new RPCILGenerator(cachedGeneratedRPCILTypeRef);
                if (!cachedOnTryStaticCallProcessor.TrySetup(typeof(RPCInterfaceRegistry.OnTryStaticCallMarker)))
                    return false;
            }

            return (rpcILGenerator = cachedOnTryStaticCallProcessor) != null;
        }

        private static void FlushCache ()
        {
            cachedGeneratedRPCILTypeRef = null;

            cachedOnTryCallProcessor = null;
            cachedOnTryStaticCallProcessor = null;
            cachedQueuedRPCILGenerator = null;

            cachedGetIsMasterMethod = null;
            cachedGetInstanceMethodRef = null;
        }
    }
}
