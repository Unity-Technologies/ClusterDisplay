using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    internal partial class RPCILPostProcessor
    {
        RPCStub[] cachedSerializedRPCS = null;

        Dictionary<string, RPCILGenerator> cachedOnTryCallProcessors = new Dictionary<string, RPCILGenerator>();
        Dictionary<string, RPCILGenerator> cachedOnTryStaticCallProcessors = new Dictionary<string, RPCILGenerator>();
        Dictionary<string, QueuedRPCILGenerator> cachedQueuedRPCILGenerators = new Dictionary<string, QueuedRPCILGenerator>();

        CodeGenDebug logger;
        CecilUtils cecilUtils;

        bool TryGetCachedGetIsEmitterMarkerMethod (out MethodInfo getIsEmitterMethod)
        {
            if (!cecilUtils.TryFindPropertyGetMethodWithAttribute<RPCBufferIO.IsEmitterMarker>(typeof(RPCBufferIO), out getIsEmitterMethod))
            {
                getIsEmitterMethod = null;
                return false;
            }

            return true;
        }

        bool TryGetGetInstanceMethodRef (ModuleDefinition moduleDef, out MethodReference getInstanceMethodRef)
        {
            if (!cecilUtils.TryFindMethodWithAttribute<SceneObjectsRegistry.GetInstanceMarker>(typeof(SceneObjectsRegistry), out var methodInfo))
            {
                getInstanceMethodRef = null;
                return false;
            }

            if (!cecilUtils.TryImport(moduleDef, methodInfo, out var methodRef))
            {
                getInstanceMethodRef = null;
                return false;
            }

            getInstanceMethodRef = methodRef;
            return true;
        }

        bool TryGetCachedQueuedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out QueuedRPCILGenerator queuedRPCILGenerator)
        {
            if (cachedQueuedRPCILGenerators.TryGetValue(compiledAssemblyDef.FullName, out queuedRPCILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            queuedRPCILGenerator = new QueuedRPCILGenerator(this, cachedGeneratedRPCILTypeRef);
            if (!queuedRPCILGenerator.TrySetup())
                return false;

            cachedQueuedRPCILGenerators.Add(compiledAssemblyDef.FullName, queuedRPCILGenerator);

            return true;
        }

        bool TryGetCachedRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            if (cachedOnTryCallProcessors.TryGetValue(compiledAssemblyDef.FullName, out rpcILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            rpcILGenerator = new RPCILGenerator(this, cachedGeneratedRPCILTypeRef);
            if (!rpcILGenerator.TrySetup(typeof(RPCInterfaceRegistry.OnTryCallInstanceImplementationAttribute)))
                return false;

            cachedOnTryCallProcessors.Add(compiledAssemblyDef.FullName, rpcILGenerator);

            return true;
        }

        bool TryGetCachedStaticRPCILGenerator (AssemblyDefinition compiledAssemblyDef, out RPCILGenerator rpcILGenerator)
        {
            if (cachedOnTryStaticCallProcessors.TryGetValue(compiledAssemblyDef.FullName, out rpcILGenerator))
                return true;

            TypeReference cachedGeneratedRPCILTypeRef = compiledAssemblyDef.Modules.Select(module => module.GetType(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName)).FirstOrDefault();
            if (cachedGeneratedRPCILTypeRef == null)
                if (!TryGenerateRPCILTypeInCompiledAssembly(compiledAssemblyDef, out cachedGeneratedRPCILTypeRef))
                    return false;

            rpcILGenerator = new RPCILGenerator(this, cachedGeneratedRPCILTypeRef);
            if (!rpcILGenerator.TrySetup(typeof(RPCInterfaceRegistry.OnTryCallStaticImplementationAttribute)))
                return false;

            cachedOnTryStaticCallProcessors.Add(compiledAssemblyDef.FullName, rpcILGenerator);

            return true;
        }

        void FlushCache ()
        {
            cachedOnTryCallProcessors.Clear();
            cachedOnTryStaticCallProcessors.Clear();
            cachedQueuedRPCILGenerators.Clear();
        }
    }
}
