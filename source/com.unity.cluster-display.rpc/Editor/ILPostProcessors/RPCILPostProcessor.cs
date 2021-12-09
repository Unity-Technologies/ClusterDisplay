using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private IEnumerable<MethodReference> GetMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef, out string rpcMethodAttributeFullName)
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            var attributeFullName = rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;
            ConcurrentQueue<MethodReference> queuedMethodDefs = new ConcurrentQueue<MethodReference>();

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

            return queuedMethodDefs.ToArray();
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
            string rpcHash, 
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
                        rpcHash,
                        rpcExecutionStage,
                        il,
                        ref afterInstruction,
                        appendRPCCMethodRef,
                        totalSizeOfStaticallySizedRPCParameters)

                    :

                    TryInjectBridgeToDynamicallySizedRPCPropagation(
                        rpcEmitterType,
                        rpcHash,
                        rpcExecutionStage,
                        il,
                        ref afterInstruction,
                        appendRPCCMethodRef,
                        totalSizeOfStaticallySizedRPCParameters);
        }

        private bool ProcessMethodDef (
            AssemblyDefinition compiledAssemblyDef,
            MethodReference targetMethodRef,
            RPCExecutionStage rpcExecutionStage,
            ref string explicitRPCHash)
        {
            CodeGenDebug.Log($"Attempting to post process method: \"{targetMethodRef.Name}\" declared in: \"{targetMethodRef.DeclaringType.Name}\" with execution stage: {rpcExecutionStage}");
            var rpcHash = CecilUtils.ComputeMethodHash(targetMethodRef);
            if (!string.IsNullOrEmpty(explicitRPCHash))
                rpcHash = explicitRPCHash;
            else explicitRPCHash = rpcHash;
            
            if (!TryInjectRPCInterceptIL(
                rpcHash,
                (ushort)rpcExecutionStage,
                targetMethodRef,
                out var modifiedTargetMethodDef))
                goto failure;

            if (modifiedTargetMethodDef.IsStatic)
            {
                if (!TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out var cachedOnTryStaticCallProcessor))
                    goto failure;

                if (!cachedOnTryStaticCallProcessor.TryAppendStaticRPCExecution(modifiedTargetMethodDef, rpcHash))
                    goto failure;
            }

            else
            {
                if (!TryGetCachedRPCILGenerator(compiledAssemblyDef, out var cachedOnTryCallProcessor))
                    goto failure;

                if (!cachedOnTryCallProcessor.TryAppendInstanceRPCExecution(modifiedTargetMethodDef, rpcHash))
                    goto failure;
            }

            if (!TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out var queuedRPCILGenerator))
                goto failure;

            if (!queuedRPCILGenerator.TryInjectILToExecuteQueuedRPC(targetMethodRef, rpcExecutionStage, rpcHash))
                goto failure;

            return true;

            failure:
            throw new Exception($"Failure occurred while attempting to post process method: \"{targetMethodRef.Name}\" in class: \"{targetMethodRef.DeclaringType.FullName}\".");
        }

        private void ApplyRPCExecutionStage(CustomAttribute customAttribute, RPCExecutionStage rpcExecutionStage) =>
            customAttribute.ConstructorArguments[0] = new CustomAttributeArgument(customAttribute.ConstructorArguments[0].Type, rpcExecutionStage);
        
        private void ApplyRPCHash(CustomAttribute customAttribute, string rpcHash) =>
            customAttribute.ConstructorArguments[1] = new CustomAttributeArgument(customAttribute.ConstructorArguments[1].Type, rpcHash);

        private bool PostProcessDeserializedRPCs (
            AssemblyDefinition compiledAssemblyDef, 
            RPCStub[] serializedRPCs)
        {
            bool modified = false;
            foreach (var serializedRPC in serializedRPCs)
            {
                var rpc = serializedRPC;

                // If the declaring assembly name does not match our compiled one, then ignore it as the RPC is probably in another assembly.
                if (rpc.methodStub.declaringAssemblyName == compiledAssemblyDef.Name.Name)
                {
                    if (!CecilUtils.TryGetTypeDefByName(compiledAssemblyDef.MainModule, rpc.methodStub.declaringTypeNamespace, rpc.methodStub.declaringTypeName, out var declaringTypeDef))
                    {
                        CodeGenDebug.LogError($"Unable to find serialized method: \"{rpc.methodStub.methodName}\", the declaring type: \"{rpc.methodStub.declaringTypeName}\" does not exist in the compiled assembly: \"{compiledAssemblyDef.Name}\".");
                        continue;
                    }
                    
                    MethodReference targetMethodRef = null;

                    bool unableToFindAnyMethod =
                        !TryFindMethodWithMatchingFormalySerializedAs(
                            compiledAssemblyDef.MainModule,
                            declaringTypeDef,
                            rpc,
                            rpc.methodStub.methodName,
                            out targetMethodRef) &&
                        !CecilUtils.TryGetMethodReference(compiledAssemblyDef.MainModule, declaringTypeDef, ref rpc, out targetMethodRef);

                    if (unableToFindAnyMethod)
                        continue;

                    if (MethodAlreadyProcessed(targetMethodRef))
                        continue;
                    
                    if (!TryGetOrCreateClusterRPCAttribute(targetMethodRef, out var customAttribute) || !TryGetRPCHashFromClusterRPCAttribute(customAttribute, out var rpcHash))
                        continue;

                    var executionStage = (RPCExecutionStage)serializedRPC.rpcExecutionStage;
                    
                    var methodModified = ProcessMethodDef(
                        compiledAssemblyDef,
                        targetMethodRef,
                        executionStage,
                        ref rpcHash);

                    if (methodModified)
                    {
                        if (serializedRPC.overrideRPCExecutionStage)
                            ApplyRPCExecutionStage(customAttribute, executionStage);
                        ApplyRPCHash(customAttribute, rpcHash);
                        AddProcessedMethod(targetMethodRef);
                    }
                    
                    modified |= methodModified;
                }
            }

            return modified;
        }

        private bool PostProcessSerializedRPCs (AssemblyDefinition compiledAssemblyDef)
        {
            if (cachedSerializedRPCS == null)
                RPCSerializer.ReadAllRPCs(
                    RPCRegistry.k_RPCStubsFileName, 
                    RPCRegistry.k_RPCStagedPath, 
                    out cachedSerializedRPCS, 
                    out var serializedStagedMethods, 
                    logMissingFile: false);

            if (cachedSerializedRPCS == null || cachedSerializedRPCS.Length == 0)
                return false;

            return PostProcessDeserializedRPCs(compiledAssemblyDef, cachedSerializedRPCS.ToArray());
        }

        private CustomAttribute GetClusterRPCAttribute(MethodReference methodRef) =>
            methodRef.Resolve().CustomAttributes.First(ca => ca.AttributeType.FullName == GetCustomAttributeTypeFullName());

        private string GetCustomAttributeTypeFullName()
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            return rpcMethodCustomAttributeType.FullName;
        }

        private bool TryGetOrCreateClusterRPCAttribute(MethodReference methodRef, out CustomAttribute customAttribute)
        {
            customAttribute = GetClusterRPCAttribute(methodRef);
            if (customAttribute != null)
                return true;
            return (customAttribute = CecilUtils.AddCustomAttributeToMethod<ClusterRPC>(methodRef.Module, methodRef.Resolve())) == null;
        }

        private bool TryGetRPCHashFromClusterRPCAttribute(CustomAttribute customAttribute, out string rpcHash)
        {
            rpcHash = (string)customAttribute.ConstructorArguments[1].Value;
            return true;
        }
        
        private bool PostProcessMethodsWithRPCAttributes (AssemblyDefinition compiledAssemblyDef)
        {
            var methodRefs = GetMethodsWithRPCAttribute(compiledAssemblyDef, out var rpcMethodAttributeFullName);
            
            var msg = $"Methods with {nameof(ClusterRPC)} attribute:";
            foreach (var methodRef in methodRefs)
                msg = $"{msg}\n\t{methodRef.DeclaringType.Name}.{methodRef.Name}";
            CodeGenDebug.Log(msg);
            
            bool modified = false;
            foreach (var methodRef in methodRefs)
            {
                var methodDef = methodRef.Resolve();
                if (methodDef == null)
                    continue;

                if (MethodAlreadyProcessed(methodDef))
                    continue;

                if (!TryGetOrCreateClusterRPCAttribute(methodRef, out var customAttribute) || !TryGetRPCHashFromClusterRPCAttribute(customAttribute, out var rpcHash))
                    continue;

                if (methodDef.IsAbstract)
                {
                    CodeGenDebug.LogError($"Instance method: \"{methodDef.Name}\" declared in type: \"{methodDef.DeclaringType.Namespace}.{methodDef.DeclaringType.Name}\" is unsupported because the type is abstract.");
                    continue;
                }
                
                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[0];
                RPCExecutionStage executionStage = (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value;

                var methodModified = ProcessMethodDef(
                    compiledAssemblyDef,
                    methodRef,
                    executionStage,
                    ref rpcHash);

                if (methodModified)
                {
                    ApplyRPCHash(customAttribute, rpcHash);
                    AddProcessedMethod(methodDef);
                }

                modified |= methodModified;
            }

            return modified;
        }

        public bool Execute(AssemblyDefinition compiledAssemblyDef)
        {
            bool modified = 
                PostProcessSerializedRPCs(compiledAssemblyDef) |
                PostProcessMethodsWithRPCAttributes(compiledAssemblyDef);

            if (modified)
            {
                InjectDefaultSwitchCases(compiledAssemblyDef);
                FlushCache();
            }
            
            return modified;
        }
    }
}
