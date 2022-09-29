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
    internal partial class RPCILPostProcessor
    {
        public RPCILPostProcessor (CecilUtils cecilUtils, CodeGenDebug logger)
        {
            this.cecilUtils = cecilUtils;
            this.logger = logger;
        }

        IEnumerable<MethodReference> GetMethodsWithRPCAttribute (AssemblyDefinition compiledAssemblyDef, out string rpcMethodAttributeFullName)
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            var attributeFullName = rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;
            ConcurrentQueue<MethodReference> queuedMethodDefs = new ConcurrentQueue<MethodReference>();

            for (int moduleIndex = 0; moduleIndex < compiledAssemblyDef.Modules.Count; moduleIndex++)
            {
                Parallel.ForEach(compiledAssemblyDef.Modules[moduleIndex].Types, type =>
                {
                    if (type == null)
                        return;

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
                });
            }

            return queuedMethodDefs.ToArray();
        }

        bool TryGetRPCILGenerators (
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

        void InjectDefaultSwitchCases (AssemblyDefinition compiledAssemblyDef)
        {
            if (TryGetCachedRPCILGenerator(compiledAssemblyDef, out var cachedOnTryCallProcessor))
                cachedOnTryCallProcessor.InjectDefaultSwitchCase();

            if (TryGetCachedStaticRPCILGenerator(compiledAssemblyDef, out var cachedOnTryStaticCallProcessor))
                cachedOnTryStaticCallProcessor.InjectDefaultSwitchCase();

            if (TryGetCachedQueuedRPCILGenerator(compiledAssemblyDef, out var cachedQueuedRPCILGenerator))
                cachedQueuedRPCILGenerator.InjectDefaultSwitchCase();
        }

        Dictionary<uint, HashSet<uint>> processedMethods = new Dictionary<uint, HashSet<uint>>();
        bool MethodAlreadyProcessed (MethodReference methodRef) => 
            processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash) &&
            hash.Contains(methodRef.MetadataToken.RID);

        void AddProcessedMethod (MethodReference methodRef)
        {
            if (!processedMethods.TryGetValue(methodRef.DeclaringType.MetadataToken.RID, out var hash))
            {
                processedMethods.Add(methodRef.DeclaringType.MetadataToken.RID, new HashSet<uint>() { methodRef.MetadataToken.RID });
                return;
            }

            hash.Add(methodRef.MetadataToken.RID);
        }
        bool TryInjectRPCInterceptIL (
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
                if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;
            }

            else if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                return false;

            if (!cecilUtils.TryImport(targetMethodRef.Module, appendRPCMethodInfo, out var appendRPCCMethodRef))
                return false;

            if (!TryGetCachedGetIsEmitterMarkerMethod(out var getIsEmitterMethod))
                return false;

            if (!TryPollParameterInformation(
                modifiedTargetMethodDef.Module,
                modifiedTargetMethodDef,
                out var totalSizeOfStaticallySizedRPCParameters,
                out var hasDynamicallySizedRPCParameters))
                return false;

            if (!cecilUtils.TryImport(targetMethodRef.Module, getIsEmitterMethod, out var getIsEmitterMethodRef))
                return false;

            var afterInstruction = cecilUtils.InsertCallBefore(il, beforeInstruction, getIsEmitterMethodRef);
            cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Brfalse, beforeInstruction);

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

        bool ProcessMethodDef (
            AssemblyDefinition compiledAssemblyDef,
            MethodReference targetMethodRef,
            RPCExecutionStage rpcExecutionStage,
            ref string explicitRPCHash)
        {
            logger.Log($"Attempting to post process method: \"{targetMethodRef.Name}\" declared in: \"{targetMethodRef.DeclaringType.Name}\" with execution stage: {rpcExecutionStage}");
            var rpcHash = cecilUtils.ComputeMethodHash(targetMethodRef);
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

        void ApplyRPCExecutionStage(CustomAttribute customAttribute, RPCExecutionStage rpcExecutionStage) =>
            customAttribute.ConstructorArguments[0] = new CustomAttributeArgument(customAttribute.ConstructorArguments[0].Type, rpcExecutionStage);
        
        void ApplyRPCHash(CustomAttribute customAttribute, string rpcHash) =>
            customAttribute.ConstructorArguments[1] = new CustomAttributeArgument(customAttribute.ConstructorArguments[1].Type, rpcHash);

        bool PostProcessDeserializedRPCs (
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
                    if (!cecilUtils.TryGetTypeDefByName(compiledAssemblyDef.MainModule, rpc.methodStub.declaringTypeNamespace, rpc.methodStub.declaringTypeName, out var declaringTypeDef))
                    {
                        logger.LogError($"Unable to find serialized method: \"{rpc.methodStub.methodName}\", the declaring type: \"{rpc.methodStub.declaringTypeName}\" does not exist in the compiled assembly: \"{compiledAssemblyDef.Name}\".");
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
                        !cecilUtils.TryGetMethodReference(compiledAssemblyDef.MainModule, declaringTypeDef, ref rpc, out targetMethodRef);

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

        bool PostProcessSerializedRPCs (AssemblyDefinition compiledAssemblyDef)
        {
            logger.Log($"Polling serialized RPCs for assembly.");
            RPCSerializer rpcSerializer = new RPCSerializer(logger);
            if (cachedSerializedRPCS == null)
                rpcSerializer.ReadAllRPCs(
                    RPCRegistry.k_RPCStubsFileName, 
                    RPCRegistry.k_RPCStagedPath, 
                    out cachedSerializedRPCS, 
                    out var serializedStagedMethods, 
                    logMissingFile: false);

            if (cachedSerializedRPCS == null || cachedSerializedRPCS.Length == 0)
                return false;

            logger.Log($"Found {cachedSerializedRPCS.Length} serialized RPCs.");
            return PostProcessDeserializedRPCs(compiledAssemblyDef, cachedSerializedRPCS.ToArray());
        }

        CustomAttribute GetClusterRPCAttribute(MethodReference methodRef) =>
            methodRef.Resolve().CustomAttributes.First(ca => ca.AttributeType.FullName == GetCustomAttributeTypeFullName());

        string GetCustomAttributeTypeFullName()
        {
            var rpcMethodCustomAttributeType = typeof(ClusterRPC);
            return rpcMethodCustomAttributeType.FullName;
        }

        bool TryGetOrCreateClusterRPCAttribute(MethodReference methodRef, out CustomAttribute customAttribute)
        {
            customAttribute = GetClusterRPCAttribute(methodRef);
            if (customAttribute != null)
                return true;
            return (customAttribute = cecilUtils.AddCustomAttributeToMethod<ClusterRPC>(methodRef.Module, methodRef.Resolve())) == null;
        }

        bool TryGetRPCHashFromClusterRPCAttribute(CustomAttribute customAttribute, out string rpcHash)
        {
            rpcHash = (string)customAttribute.ConstructorArguments[1].Value;
            return true;
        }
        
        bool PostProcessMethodsWithRPCAttributes (AssemblyDefinition compiledAssemblyDef)
        {
            var methodRefs = GetMethodsWithRPCAttribute(compiledAssemblyDef, out var rpcMethodAttributeFullName);
            
            var msg = $"Methods with {nameof(ClusterRPC)} attribute:";
            foreach (var methodRef in methodRefs)
                msg = $"{msg}\n\t{methodRef.DeclaringType.Name}.{methodRef.Name}";
            logger.Log(msg);
            
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
                    logger.LogError($"Instance method: \"{methodDef.Name}\" declared in type: \"{methodDef.DeclaringType.Namespace}.{methodDef.DeclaringType.Name}\" is unsupported because the type is abstract.");
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
