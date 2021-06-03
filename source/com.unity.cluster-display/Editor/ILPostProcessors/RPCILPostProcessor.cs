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

        private static Dictionary<RPCExecutionStage, ILProcessor> cachedExecuteQueuedRPCMethodILProcessors;
        private static Dictionary<RPCExecutionStage, Instruction> executionStageLastSwitchJmpInstructions;

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

            List<ushort> usedRPCIds = new List<ushort>();
            if (RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out var serializedRPCs) && serializedRPCs.Length > 0)
            {
                foreach (var serializedRPC in serializedRPCs)
                {
                    var rpc = serializedRPC;

                    var typeDefinition = compiledAssemblyDef.MainModule.GetType(rpc.declaryingTypeFullName);
                    MethodDefinition targetRPCMethodDef = null;

                    if (!TryFindMethodWithMatchingFormalySerializedAs(
                        compiledAssemblyDef.MainModule,
                        typeDefinition,
                        rpc.methodName,
                        out targetRPCMethodDef) &&
                        !TryGetMethodDefinition(typeDefinition, ref rpc, out targetRPCMethodDef))
                    {
                        Debug.LogError($"Unable to find method signature: \"{rpc.methodName}\".");
                        goto failure;
                    }

                    if (!(targetRPCMethodDef.IsStatic ? onTryStaticCallProcessor : onTryCallProcessor).ProcessMethodDef(
                        serializedRPC.rpcId,
                        targetRPCMethodDef,
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
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
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
