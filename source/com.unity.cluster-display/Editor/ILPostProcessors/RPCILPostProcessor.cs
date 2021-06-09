using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public partial class RPCILPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => this;

        public bool TryInjectDefaultSwitchCaseForExecutionStageMethods()
        {
            if (cachedExecuteQueuedRPCMethodILProcessors != null)
            {
                foreach (var cachedExecutedRPCMethodILProcessor in cachedExecuteQueuedRPCMethodILProcessors)
                {
                    if (!executionStageLastSwitchJmpInstructions.TryGetValue(cachedExecutedRPCMethodILProcessor.Key, out var lastExecuteQueuedRPCJmpInstruction))
                        continue;

                    var isntructionToJmpTo = cachedExecutedRPCMethodILProcessor.Value.Body.Instructions[cachedExecutedRPCMethodILProcessor.Value.Body.Instructions.Count - 2];
                    var newInstruction = Instruction.Create(OpCodes.Br, isntructionToJmpTo);
                    cachedExecutedRPCMethodILProcessor.Value.InsertAfter(lastExecuteQueuedRPCJmpInstruction, newInstruction);
                }
            }

            return true;
        }

        private static void CachMethodReferencesInMethodInstructions (MethodDefinition callingMethodDef)
        {
            if (!cachedCallTree.TryGetValue(callingMethodDef.MetadataToken, out var call))
            {
                call = new Call
                {
                    callingMethods = new List<MetadataToken>(),
                    calledMethodRef = callingMethodDef,
                    methodsCalled = new List<MetadataToken>()
                };

                cachedCallTree.Add(callingMethodDef.MetadataToken, call);
            }

            for (int ii = 0; ii < callingMethodDef.Body.Instructions.Count; ii++)
            {
                var calledMethodDef = callingMethodDef.Body.Instructions[ii].Operand as MethodReference;
                if (calledMethodDef == null)
                    continue;

                if (!cachedCallTree.TryGetValue(calledMethodDef.MetadataToken, out var called))
                    cachedCallTree.Add(calledMethodDef.MetadataToken, new Call
                    {
                        callingMethods = new List<MetadataToken>() { callingMethodDef.MetadataToken },
                        calledMethodRef = calledMethodDef,
                        methodsCalled = new List<MetadataToken>()
                    });

                else called.callingMethods.Add(callingMethodDef.MetadataToken);

                call.methodsCalled.Add(calledMethodDef.MetadataToken);
            }
        }

        private static void CacheCallTree (ModuleDefinition moduleDef)
        {
            var coroutineTypeRef = moduleDef.ImportReference(typeof(System.Collections.IEnumerator));
            for (int ti = 0; ti < moduleDef.Types.Count; ti++)
            {
                for (int mi = 0; mi < moduleDef.Types[ti].Methods.Count; mi++)
                {
                    if (moduleDef.Types[ti].Methods[mi].Body == null)
                        continue;

                    var callingMethodDef = moduleDef.Types[ti].Methods[mi];

                    bool isCoroutine =
                        callingMethodDef.ReturnType.MetadataToken == coroutineTypeRef.MetadataToken &&
                        callingMethodDef.DeclaringType.HasNestedTypes;

                    if (isCoroutine)
                        for (int ni = 0; ni < callingMethodDef.DeclaringType.NestedTypes.Count; ni++)
                            for (int nmi = 0; nmi < callingMethodDef.DeclaringType.NestedTypes[ni].Methods.Count; nmi++)
                                CachMethodReferencesInMethodInstructions(callingMethodDef.DeclaringType.NestedTypes[ni].Methods[nmi]);

                    CachMethodReferencesInMethodInstructions(callingMethodDef);
                }
            }
        }

        private static void CacheExecutionStageMethods ()
        {
            var monoBehaviourType = typeof(MonoBehaviour);
            cachedMonoBehaviourMethodSignaturesForRPCExecutionStages.Add($"{monoBehaviourType.Namespace}.{monoBehaviourType.Name}.FixedUpdate", RPCExecutionStage.AfterFixedUpdate);
            cachedMonoBehaviourMethodSignaturesForRPCExecutionStages.Add($"{monoBehaviourType.Namespace}.{monoBehaviourType.Name}.Update", RPCExecutionStage.AfterUpdate);
            cachedMonoBehaviourMethodSignaturesForRPCExecutionStages.Add($"{monoBehaviourType.Namespace}.{monoBehaviourType.Name}.LateUpdate", RPCExecutionStage.AfterLateUpdate);
            cachedMonoBehaviourMethodSignaturesForRPCExecutionStages.Add($"{monoBehaviourType.Namespace}.{monoBehaviourType.Name}.OnGUI", RPCExecutionStage.AfterLateUpdate);
        }

        private static bool MethodIsCoroutine (MethodDefinition methodDef) => methodDef.ReturnType.MetadataToken == methodDef.Module.ImportReference(typeof(System.Collections.IEnumerator)).MetadataToken;

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name != ReflectionUtils.DefaultUserAssemblyName)
                goto ignoreAssembly;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out var compiledAssemblyDef))
                goto failure;

            var rpcInterfaceRegistryType = typeof(RPCInterfaceRegistry);
            var rpcInstanceRegistryTypeDef = compiledAssemblyDef.MainModule.ImportReference(rpcInterfaceRegistryType).Resolve();

            if (!TryGetDerrivedType(
                compiledAssemblyDef,
                rpcInstanceRegistryTypeDef, 
                out var rpcInterfaceRegistryDerrivedTypeRef))
                goto failure;

            var onTryCallProcessor = new RPCExecutionILGenerator();
            if (!onTryCallProcessor.TrySetup(
                compiledAssemblyDef,
                rpcInterfaceRegistryDerrivedTypeRef,
                typeof(RPCInterfaceRegistry.OnTryCallMarker)))
                goto failure;

            var onTryStaticCallProcessor = new RPCExecutionILGenerator();
            if (!onTryStaticCallProcessor.TrySetup(
                compiledAssemblyDef,
                rpcInterfaceRegistryDerrivedTypeRef,
                typeof(RPCInterfaceRegistry.OnTryStaticCallMarker)))
                goto failure;

            // CacheCallTree(compiledAssemblyDef.MainModule);
            // CacheExecutionStageMethods();

            List<ushort> usedRPCIds = new List<ushort>();
            if (RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out var serializedRPCs) && serializedRPCs.Length > 0)
            {
                foreach (var serializedRPC in serializedRPCs)
                {
                    var rpc = serializedRPC;

                    var typeDefinition = compiledAssemblyDef.MainModule.GetType(rpc.declaryingTypeFullName);
                    MethodReference targetMethodRef = null;

                    if (!TryFindMethodWithMatchingFormalySerializedAs(
                        compiledAssemblyDef.MainModule,
                        typeDefinition,
                        rpc.methodName,
                        out targetMethodRef) &&
                        !TryGetMethodReference(compiledAssemblyDef.MainModule, typeDefinition, ref rpc, out targetMethodRef))
                    {
                        Debug.LogError($"Unable to find method signature: \"{rpc.methodName}\".");
                        goto failure;
                    }

                    var targetMethodDef = targetMethodRef.Resolve();
                    if (!(targetMethodDef.IsStatic ? onTryStaticCallProcessor : onTryCallProcessor).ProcessMethodDef(
                        serializedRPC.rpcId,
                        targetMethodRef,
                        (RPCExecutionStage)serializedRPC.rpcExecutionStage))
                        goto failure;

                    // Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

                    usedRPCIds.Add(rpc.rpcId);
                }
            }

            Queue<ushort> unusedRPCIds = new Queue<ushort>();
            int lastRPCId = -1;

            if (usedRPCIds.Count > 0)
            {
                usedRPCIds.Sort();
                lastRPCId = usedRPCIds.Last();
                for (ushort rpcId = 0; rpcId < lastRPCId; rpcId++)
                {
                    if (usedRPCIds.Contains(rpcId))
                        continue;
                    unusedRPCIds.Enqueue(rpcId);
                }
            }

            var rpcMethodCustomAttributeType = typeof(RPCMethod);

            var rpcMethodCustomAttributeTypeRef = compiledAssemblyDef.MainModule.ImportReference(rpcMethodCustomAttributeType);
            string rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;

            var rpcMethodAttributeRPCExecutionStageArgument = compiledAssemblyDef.MainModule.ImportReference(typeof(RPCExecutionStage));
            var methodDefs = compiledAssemblyDef.Modules
                .SelectMany(moduleDef => moduleDef.Types
                    .SelectMany(type => type.Methods
                        .Where(method => method.CustomAttributes
                            .Any(customAttribute => 
                            customAttribute.HasConstructorArguments &&
                            customAttribute.AttributeType.FullName == rpcMethodAttributeFullName))));

            foreach (var targetRPCMethodDef in methodDefs)
            {
                // Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

                var customAttribute = targetRPCMethodDef.CustomAttributes.First(ca => ca.AttributeType.FullName == rpcMethodAttributeFullName);
                if (!TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPCMethod.RPCExecutionStageMarker>(customAttribute, out var rpcExecutionStageAttributeArgumentIndex) ||
                    !TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPCMethod.RPCIDMarker>(customAttribute, out var rpcIdAttributeArgumentIndex))
                    goto failure;

                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[rpcExecutionStageAttributeArgumentIndex];

                ushort newRPCId = unusedRPCIds.Count > 0 ? unusedRPCIds.Dequeue() : (ushort)++lastRPCId;

                if (!(targetRPCMethodDef.IsStatic ? onTryStaticCallProcessor : onTryCallProcessor).ProcessMethodDef(
                    newRPCId,
                    targetRPCMethodDef,
                    (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value))
                    goto failure;

                var customAttributeArgument = customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex];
                customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex] = new CustomAttributeArgument(customAttributeArgument.Type, newRPCId);
            }

            if (!onTryCallProcessor.TryInjectSwitchCaseForImmediateRPCExecutionMethod() &&
                !onTryStaticCallProcessor.TryInjectSwitchCaseForImmediateRPCExecutionMethod() &&
                !TryInjectDefaultSwitchCaseForExecutionStageMethods())
                goto failure;

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), 
                SymbolStream = pdb, 
                WriteSymbols = true,
            };

            try
            {
                compiledAssemblyDef.Write(pe, writerParameters);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                goto failure;
            }

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));

            ignoreAssembly:
            return null;

            failure:
            Debug.LogError($"Failure occurred while attempting to post process assembly: \"{compiledAssembly.Name}\".");
            return null;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
