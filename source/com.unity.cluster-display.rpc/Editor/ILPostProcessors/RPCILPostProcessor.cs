using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private IEnumerable<MethodDefinition> GetMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef, out string rpcMethodAttributeFullName)
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            var attributeFullName = rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;
            ConcurrentQueue<MethodDefinition> queuedMethodDefs = new ConcurrentQueue<MethodDefinition>();

            for (int moduleIndex = 0; moduleIndex < compiledAssemblyDef.Modules.Count; moduleIndex++)
            {
                int typeCount = compiledAssemblyDef.Modules[moduleIndex].Types.Count;

                int workerCount = Environment.ProcessorCount < typeCount ? Environment.ProcessorCount : typeCount;
                int typeCountPerWorker = typeCount / workerCount;
                int remainder = typeCount % workerCount;

                Parallel.For(0, workerCount, workerId =>
                {
                    int start = typeCountPerWorker * workerId;
                    int end = typeCountPerWorker * (workerId + 1);
                    if (workerId == Environment.ProcessorCount - 1)
                        end += remainder;

                    // LogWriter.Log($"Worker: {workerId}, Start: {start}, End: {end}, Type Count: {typeCount}");

                    var types = compiledAssemblyDef.Modules[moduleIndex].Types;
                    for (int typeIndex = start; typeIndex < end; typeIndex++)
                    {
                        var type = types[typeIndex];
                        if (type == null)
                            continue;

                        var methods = type.Methods;
                        for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
                        {
                            var method = methods[methodIndex];
                            if (method == null)
                                continue;

                            var customAttributes = method.CustomAttributes;
                            for (int caIndex = 0; caIndex < customAttributes.Count; caIndex++)
                            {
                                var customAttribute = customAttributes[caIndex];

                                if (customAttribute.AttributeType.FullName != attributeFullName)
                                {
                                    continue;
                                    // If the custom attribute does not match, then check it's base type as this attribute may be an
                                    // obsolete version of the RPC attribute that derrives from the current one.
                                    /* This was commented out since it was causing problems in latest version of ILPP.
                                    var attributeType = customAttribute.AttributeType.Resolve();
                                    if (attributeType.BaseType.FullName != attributeFullName)
                                        continue;
                                    */
                                }

                                queuedMethodDefs.Enqueue(method);
                            }
                        }
                    }
                });
            }

            return queuedMethodDefs.OrderBy(methodDef => methodDef.FullName);
        }

        private bool TryGetRPCILGenerators (
            AssemblyDefinition compiledAssemblyDef, 
            MethodDefinition targetMethodDef,
            out RPCILGenerator rpcILGenerator, 
            out QueuedRPCILGenerator queuedRPCILGenerator)
        {
            rpcILGenerator = null;
            queuedRPCILGenerator = null;

            if (targetMethodDef.IsStatic)
            {
                if (!TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out rpcILGenerator))
                    return false;
            }

            else if (!TryGetCachedRPCILGenerator(compiledAssemblyDef, out rpcILGenerator))
                return false;

            if (!TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out queuedRPCILGenerator))
                return false;

            return true;
        }

        private void InjectDefaultSwitchCases (AssemblyDefinition compiledAssemblyDef)
        {
            if (TryGetCachedRPCILGenerator(compiledAssemblyDef, out var cachedOnTryCallProcessor))
                cachedOnTryCallProcessor.InjectDefaultSwitchCase();

            if (TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out var cachedOnTryStaticCallProcessor))
                cachedOnTryStaticCallProcessor.InjectDefaultSwitchCase();

            if (TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out var cachedQueuedRPCILGenerator))
                cachedQueuedRPCILGenerator.InjectDefaultSwitchCase();
        }

        private Dictionary<uint, HashSet<uint>> processedMethods = new Dictionary<uint, HashSet<uint>>();
        private bool MethodAlreadyProcessed (MethodReference methodRef) => 
            processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash) &&
            hash.Contains(methodRef.MetadataToken.RID);

        private void AddProcessedMethod (MethodReference methodRef)
        {
            if (!processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash))
            {
                processedMethods.Add(methodRef.DeclaringType.MetadataToken.RID, new HashSet<uint>() { methodRef.MetadataToken.RID });
                return;
            }

            hash.Add(methodRef.MetadataToken.RID);
        }
        private bool TryInjectRPCInterceptIL (
            ushort rpcId, 
            ushort rpcExecutionStage,
            MethodReference targetMethodRef,
            out MethodDefinition modifiedTargetMethodDef)
        {
            modifiedTargetMethodDef = targetMethodRef.Resolve();

            var beforeInstruction = modifiedTargetMethodDef.Body.Instructions.First();
            var il = modifiedTargetMethodDef.Body.GetILProcessor();

            var rpcEmitterType = typeof(RPCBufferIO);

            MethodInfo appendRPCMethodInfo = null;
            if (!modifiedTargetMethodDef.IsStatic)
            {
                if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;
            }

            else if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                return false;

            if (!CecilUtils.TryImport(targetMethodRef.Module, appendRPCMethodInfo, out var appendRPCCMethodRef))
                return false;

            if (!TryGetCachedGetIsEmitterMarkerMethod(out var getIsEmitterMethod))
                return false;

            if (!TryPollParameterInformation(
                modifiedTargetMethodDef.Module,
                modifiedTargetMethodDef,
                out var totalSizeOfStaticallySizedRPCParameters,
                out var hasDynamicallySizedRPCParameters))
                return false;

            if (!CecilUtils.TryImport(targetMethodRef.Module, getIsEmitterMethod, out var getIsEmitterMethodRef))
                return false;

            var afterInstruction = CecilUtils.InsertCallBefore(il, beforeInstruction, getIsEmitterMethodRef);
            CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Brfalse, beforeInstruction);

            return !hasDynamicallySizedRPCParameters ?

                    TryInjectBridgeToStaticallySizedRPCPropagation(
                        rpcEmitterType,
                        rpcId,
                        rpcExecutionStage,
                        il,
                        ref afterInstruction,
                        appendRPCCMethodRef,
                        totalSizeOfStaticallySizedRPCParameters)

                    :

                    TryInjectBridgeToDynamicallySizedRPCPropagation(
                        rpcEmitterType,
                        rpcId,
                        rpcExecutionStage,
                        il,
                        ref afterInstruction,
                        appendRPCCMethodRef,
                        totalSizeOfStaticallySizedRPCParameters);
        }

        private bool ProcessMethodDef (
            AssemblyDefinition compiledAssemblyDef,
            ushort rpcId,
            MethodReference targetMethodRef,
            RPCExecutionStage rpcExecutionStage)
        {
            if (!TryInjectRPCInterceptIL(
                rpcId,
                (ushort)rpcExecutionStage,
                targetMethodRef,
                out var modifiedTargetMethodDef))
                goto failure;

            if (modifiedTargetMethodDef.IsStatic)
            {
                if (!TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out var cachedOnTryStaticCallProcessor))
                    goto failure;

                if (!cachedOnTryStaticCallProcessor.TryAppendStaticRPCExecution(modifiedTargetMethodDef, rpcId))
                    goto failure;
            }

            else
            {
                if (!TryGetCachedRPCILGenerator(compiledAssemblyDef, out var cachedOnTryCallProcessor))
                    goto failure;

                if (!cachedOnTryCallProcessor.TryAppendInstanceRPCExecution(modifiedTargetMethodDef, rpcId))
                    goto failure;
            }

            if (!TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out var queuedRPCILGenerator))
                goto failure;

            if (!queuedRPCILGenerator.TryInjectILToExecuteQueuedRPC(targetMethodRef, rpcExecutionStage, rpcId))
                goto failure;

            return true;

            failure:
            CodeGenDebug.LogError($"Failure occurred while attempting to post process method: \"{targetMethodRef.Name}\" in class: \"{targetMethodRef.DeclaringType.FullName}\".");
            return false;
        }

        private bool TryProcessSerializedRPCs (
            AssemblyDefinition compiledAssemblyDef, 
            SerializedRPC[] serializedRPCs)
        {
            var orderedSerializedRPCs = serializedRPCs.OrderBy(serializedRPC => serializedRPC.method.methodName);
            foreach (var serializedRPC in orderedSerializedRPCs)
            {
                var rpc = serializedRPC;

                // If the declaring assembly name does not match our compiled one, then ignore it as the RPC is probably in another assembly.
                if (rpc.method.declaringAssemblyName == compiledAssemblyDef.Name.Name)
                {
                    if (rpc.method.methodName == "Init")
                        ClusterDebug.Log("TEST");
                    
                    if (!CecilUtils.TryGetTypeDefByName(compiledAssemblyDef.MainModule, rpc.method.declaringTypeNamespace, rpc.method.declaryingTypeName, out var declaringTypeDef))
                    {
                        CodeGenDebug.LogError($"Unable to find serialized method: \"{rpc.method.methodName}\", the declaring type: \"{rpc.method.declaryingTypeName}\" does not exist in the compiled assembly: \"{compiledAssemblyDef.Name}\".");
                        continue;
                    }
                    
                    MethodReference targetMethodRef = null;

                    bool unableToFindAnyMethod =
                        !TryFindMethodWithMatchingFormalySerializedAs(
                            compiledAssemblyDef.MainModule,
                            declaringTypeDef,
                            rpc,
                            rpc.method.methodName,
                            out targetMethodRef) &&
                        !CecilUtils.TryGetMethodReference(compiledAssemblyDef.MainModule, declaringTypeDef, ref rpc, out targetMethodRef);

                    if (unableToFindAnyMethod)
                        continue;

                    if (MethodAlreadyProcessed(targetMethodRef))
                        continue;

                    var executionStage = (RPCExecutionStage)serializedRPC.rpcExecutionStage;

                    if (!ProcessMethodDef(
                        compiledAssemblyDef,
                        serializedRPC.rpcId,
                        targetMethodRef,
                        executionStage))
                        return false;

                    AddProcessedMethod(targetMethodRef);
                }

                // Even if an RPC is in another assembly, we still want to store it's RPC ID to flag it's use.
                UniqueRPCIdManager.Use(rpc.rpcId);
            }

            return true;
        }

        private bool TryPollSerializedRPCs (AssemblyDefinition compiledAssemblyDef)
        {
            if (cachedSerializedRPCS == null)
                RPCSerializer.ReadRPCStubs(RPCRegistry.k_RPCStubsPath, out cachedSerializedRPCS, out var serializedStagedMethods);

            if (cachedSerializedRPCS != null && cachedSerializedRPCS.Length > 0)
                if (!TryProcessSerializedRPCs(compiledAssemblyDef, cachedSerializedRPCS.ToArray()))
                    return false;

            return true;
        }

        private void SetRPCAttributeRPCIDArgument (CustomAttribute customAttribute, int rpcIdAttributeArgumentIndex, ushort newRPCId)
        {
            var customAttributeArgument = customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex];
            customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex] = new CustomAttributeArgument(customAttributeArgument.Type, newRPCId);
        }

        private bool TryProcessMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef)
        {
            var methodDefs = GetMethodsWithRPCAttribute(compiledAssemblyDef, out var rpcMethodAttributeFullName);
            foreach (var methodDef in methodDefs)
            {
                if (methodDef == null)
                    continue;

                if (MethodAlreadyProcessed(methodDef))
                    continue;

                var customAttribute = methodDef.CustomAttributes.First(ca => ca.AttributeType.FullName == rpcMethodAttributeFullName);
                if (!CecilUtils.TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<ClusterRPC.RPCExecutionStageMarker>(customAttribute, out var rpcExecutionStageAttributeArgumentIndex) ||
                    !CecilUtils.TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<ClusterRPC.RPCIDMarker>(customAttribute, out var rpcIdAttributeArgumentIndex))
                    return false;

                if (methodDef.IsAbstract)
                {
                    CodeGenDebug.LogError($"Instance method: \"{methodDef.Name}\" declared in type: \"{methodDef.DeclaringType.Namespace}.{methodDef.DeclaringType.Name}\" is unsupported because the type is abstract.");
                    continue;
                }

                int explicitRPCId = (int)customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex].Value;
                
                ushort rpcId;
                if (explicitRPCId == -1)
                    rpcId = UniqueRPCIdManager.GetUnused();
                
                else
                {
                    if (!UniqueRPCIdManager.InUse((ushort)explicitRPCId))
                        rpcId = (ushort)explicitRPCId;
                    
                    else
                    {
                        CodeGenDebug.LogError($"There are multiple RPCs declared with an explicit RPC ID of: {explicitRPCId}, we are going to use a random one instead.");
                        rpcId = UniqueRPCIdManager.GetUnused();
                    }
                }
                
                SetRPCAttributeRPCIDArgument(customAttribute, rpcIdAttributeArgumentIndex, rpcId);
                
                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[rpcExecutionStageAttributeArgumentIndex];
                RPCExecutionStage executionStage = (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value;

                if (!ProcessMethodDef(
                    compiledAssemblyDef,
                    rpcId,
                    methodDef,
                    executionStage))
                    return false;

                AddProcessedMethod(methodDef);
            }

            return true;
        }

        public bool TryProcess(AssemblyDefinition compiledAssemblyDef)
        {
            if (!TryPollSerializedRPCs(compiledAssemblyDef))
                return false;

            if (!TryProcessMethodsWithRPCAttribute(compiledAssemblyDef))
                return false;

            InjectDefaultSwitchCases(compiledAssemblyDef);
            FlushCache();
            return true;
        }
    }
}
