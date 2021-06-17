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
        sealed class RPCExecutionILGenerator
        {
            private ModuleDefinition moduleDef;
            private ILProcessor ilProcessor;

            private Instruction firstInstruction;
            private Instruction lastInstruction;

            private Instruction lastSwitchJmpInstruction;

            public bool TrySetup (
                AssemblyDefinition compiledAssemblyDef, 
                TypeReference targetType, 
                Type methodAttributeMarker)
            {
                moduleDef = compiledAssemblyDef.MainModule;

                if (!GetRPCInstanceRegistryMethodImplementation(
                    compiledAssemblyDef,
                    targetType,
                    methodAttributeMarker, 
                    out ilProcessor))
                    return false;

                ilProcessor.Body.Instructions.Clear();
                ilProcessor.Body.Variables.Clear();
                ilProcessor.Body.InitLocals = false;

                firstInstruction = Instruction.Create(OpCodes.Nop);
                ilProcessor.Append(firstInstruction);

                lastInstruction = PushInt(0);
                ilProcessor.Append(lastInstruction);
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));

                lastSwitchJmpInstruction = firstInstruction;
                return true;
            }

            private bool TryFindMethodReferenceWithAttribute (
                TypeDefinition typeDef, 
                TypeReference attributeTypeRef, 
                out MethodReference methodRef)
            {
                MethodDefinition methodDef = null;
                var found = (methodDef = typeDef.Methods
                    .Where(method => method.CustomAttributes
                        .Any(customAttribute =>
                        {
                            return
                                customAttribute.AttributeType.FullName == attributeTypeRef.FullName;
                        })).FirstOrDefault()) != null;

                if (!found)
                {
                    Debug.LogError($"Unable to find method definition with attribute: \"{attributeTypeRef.FullName}\" in type: \"{typeDef.FullName}\".");
                    methodRef = null;
                    return false;
                }

                methodRef = moduleDef.ImportReference(methodDef);
                return true;
            }

            private bool GetRPCInstanceRegistryMethodImplementation (
                AssemblyDefinition assemblyDef, 
                TypeReference derrivedTypeRef,
                Type markerAttribute, 
                out ILProcessor il)
            {
                var onTryCallMarkerAttributeTypeRef = assemblyDef.MainModule.ImportReference(markerAttribute);

                if (!TryFindMethodReferenceWithAttribute(
                    derrivedTypeRef.Resolve(), 
                    onTryCallMarkerAttributeTypeRef, 
                    out var onTryCallMethodRef))
                {
                    il = null;
                    return false;
                }

                var methodDef = onTryCallMethodRef.Resolve();
                derrivedTypeRef = onTryCallMethodRef.DeclaringType;
                il = methodDef.Body.GetILProcessor();

                return true;
            }

            private bool TryGetGetInstanceMethodRef (out MethodReference getInstanceMethodRef)
            {
                if (cachedGetInstanceMethodRef != null)
                {
                    getInstanceMethodRef = cachedGetInstanceMethodRef;
                    return true;
                }

                if (!TryFindMethodWithAttribute<SceneObjectsRegistry.GetInstanceMarker>(typeof(SceneObjectsRegistry), out var methodInfo))
                {
                    getInstanceMethodRef = null;
                    return false;
                }

                return (getInstanceMethodRef = cachedGetInstanceMethodRef = moduleDef.ImportReference(methodInfo)) != null;
            }

            private bool TryInjectRPCInterceptIL (
                int rpcId, 
                int rpcExecutionStage,
                MethodReference targetMethodRef)
            {
                var targetMethodDef = targetMethodRef.Resolve();

                var beforeInstruction = targetMethodDef.Body.Instructions.First();
                var il = targetMethodDef.Body.GetILProcessor();

                var rpcEmitterType = typeof(RPCEmitter);
                var rpcEmitterTypeReference = targetMethodRef.Module.ImportReference(rpcEmitterType);

                MethodInfo appendRPCMethodInfo = null;
                if (!targetMethodDef.IsStatic)
                {
                    if (!TryFindMethodWithAttribute<RPCEmitter.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                        return false;
                }

                else if (!TryFindMethodWithAttribute<RPCEmitter.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;

                var appendRPCCMethodRef = targetMethodRef.Module.ImportReference(appendRPCMethodInfo);

                if (!TryGetCachedGetIsMasterMarkerMethod(out var getIsMasterMethod))
                    return false;

                if (!TryPollParameterInformation(
                    targetMethodRef.Module,
                    targetMethodRef,
                    out var totalSizeOfStaticallySizedRPCParameters,
                    out var hasDynamicallySizedRPCParameters))
                    return false;

                var afterInstruction = InsertCallBefore(il, beforeInstruction, targetMethodRef.Module.ImportReference(getIsMasterMethod));
                InsertAfter(il, ref afterInstruction, OpCodes.Brfalse, beforeInstruction);

                return !hasDynamicallySizedRPCParameters ?

                        TryInjectBridgeToStaticallySizedRPCPropagation(
                            rpcEmitterType,
                            rpcId,
                            rpcExecutionStage,
                            targetMethodRef,
                            il,
                            ref afterInstruction,
                            appendRPCCMethodRef,
                            totalSizeOfStaticallySizedRPCParameters)

                        :

                        TryInjectBridgeToDynamicallySizedRPCPropagation(
                            rpcEmitterType,
                            rpcId,
                            rpcExecutionStage,
                            targetMethodRef,
                            il,
                            ref afterInstruction,
                            appendRPCCMethodRef,
                            totalSizeOfStaticallySizedRPCParameters);
            }

            private bool InjectPushOfRPCParamters (
                ILProcessor ilProcessor,
                MethodReference targetMethodRef,
                ParameterDefinition bufferPosParamDef,
                bool isImmediateRPCExeuction,
                ref Instruction afterInstruction)
            {
                foreach (var paramDef in targetMethodRef.Parameters)
                {
                    if (paramDef.ParameterType.IsValueType)
                    {
                        if (!TryFindMethodWithAttribute<RPCEmitter.ParseStructureMarker>(typeof(RPCEmitter), out var parseStructureMethod))
                            return false;
                        var parseStructureMethodRef = moduleDef.ImportReference(parseStructureMethod);

                        var genericInstanceMethod = new GenericInstanceMethod(parseStructureMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                        var paramRef = moduleDef.ImportReference(paramDef.ParameterType);
                        paramRef.IsValueType = true;
                        genericInstanceMethod.GenericArguments.Add(paramRef);
                        var genericInstanceMethodRef = moduleDef.ImportReference(genericInstanceMethod);

                        InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        InsertCallAfter(ilProcessor, ref afterInstruction, genericInstanceMethodRef);  // Call generic method to convert bytes into our struct.
                    }

                    else if (ParameterIsString(targetMethodRef.Module, paramDef))
                    {
                        if (!TryFindMethodWithAttribute<RPCEmitter.ParseStringMarker>(typeof(RPCEmitter), out var parseStringMethod))
                            return false;

                        var parseStringMethodRef = moduleDef.ImportReference(parseStringMethod);

                        InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        InsertCallAfter(ilProcessor, ref afterInstruction, parseStringMethodRef);
                    }

                    else if (paramDef.ParameterType.IsArray)
                    {
                        var arrayElementType = paramDef.ParameterType.GetElementType();
                        if (!TryFindMethodWithAttribute<RPCEmitter.ParseArrayMarker>(typeof(RPCEmitter), out var parseArrayMethod))
                            return false;

                        var parseArrayMethodRef = moduleDef.ImportReference(parseArrayMethod);

                        var genericInstanceMethod = new GenericInstanceMethod(parseArrayMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                        var paramRef = moduleDef.ImportReference(arrayElementType);
                        paramRef.IsValueType = true;
                        genericInstanceMethod.GenericArguments.Add(paramRef);
                        var genericInstanceMethodRef = moduleDef.ImportReference(genericInstanceMethod);

                        InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        InsertCallAfter(ilProcessor, ref afterInstruction, genericInstanceMethod);
                    }
                }

                return true;
            }

            private Instruction PerformCall (MethodReference methodRef)
            {
                var methodDef = methodRef.Resolve();
                if (methodDef.IsVirtual)
                    return Instruction.Create(OpCodes.Callvirt, methodRef);
                return Instruction.Create(OpCodes.Call, methodRef);
            }

            private bool TryInjectInstanceRPCExecution (
                ILProcessor ilProcessor,
                Instruction beforeInstruction,
                MethodReference targetMethod,
                bool isImmediateRPCExeuction,
                out Instruction firstInstructionOfInjection)
            {
                var method = ilProcessor.Body.Method;
                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(method, out var pipeIdParamDef) ||
                    !TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                if (!TryGetGetInstanceMethodRef(out var getInstanceMEthodRef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                var afterInstruction = firstInstructionOfInjection = InsertPushParameterToStackBefore(ilProcessor, beforeInstruction, pipeIdParamDef, isStaticCaller: method.IsStatic, byReference: false); // Load pipeId parameter onto stack.
                InsertCallAfter(ilProcessor, ref afterInstruction, getInstanceMEthodRef);

                if (!targetMethod.HasParameters)
                {
                    // Call method on target object without any parameters.
                    InsertCallAfter(ilProcessor, ref afterInstruction, targetMethod);

                    if (isImmediateRPCExeuction)
                        InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                    InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                    return true;
                }

                InjectPushOfRPCParamters(
                    ilProcessor,
                    targetMethod,
                     bufferPosParamDef,
                     isImmediateRPCExeuction,
                     ref afterInstruction);

                InsertCallAfter(ilProcessor, ref afterInstruction, targetMethod);

                if (isImmediateRPCExeuction)
                    InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            private bool TryInjectStaticRPCExecution (
                ILProcessor ilProcessor,
                Instruction beforeInstruction,
                MethodReference targetMethodRef,
                bool isImmediateRPCExeuction,
                out Instruction firstInstructionOfInjection)
            {
                var method = ilProcessor.Body.Method;
                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                Instruction afterInstruction = null;

                var voidTypeRef = ilProcessor.Body.Method.Module.ImportReference(typeof(void));
                if (!targetMethodRef.HasParameters)
                {
                    // Call method on target object without any parameters.
                    firstInstructionOfInjection = InsertCallBefore(ilProcessor, beforeInstruction, targetMethodRef);

                    if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                        InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

                    if (isImmediateRPCExeuction)
                        InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                    InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                    return true;
                }

                afterInstruction = firstInstructionOfInjection = InsertBefore(ilProcessor, beforeInstruction, OpCodes.Nop);

                InjectPushOfRPCParamters(
                    ilProcessor,
                    targetMethodRef,
                     bufferPosParamDef,
                     isImmediateRPCExeuction,
                     ref afterInstruction);

                InsertCallAfter(ilProcessor, ref afterInstruction, targetMethodRef);

                if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                    InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

                if (isImmediateRPCExeuction)
                    InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            private bool TryGetTryCallParameters (
                out ParameterDefinition pipeIdParamDef,
                out ParameterDefinition rpcIdParamDef,
                out ParameterDefinition parametersPayloadSizeParamDef,
                out ParameterDefinition rpcBufferPositionParamDef)
            {
                pipeIdParamDef = null;
                rpcIdParamDef = null;
                parametersPayloadSizeParamDef = null;
                rpcBufferPositionParamDef = null;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(ilProcessor.Body.Method, out pipeIdParamDef))
                    return false;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out rpcIdParamDef))
                    return false;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(ilProcessor.Body.Method, out parametersPayloadSizeParamDef))
                    return false;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(ilProcessor.Body.Method, out rpcBufferPositionParamDef))
                    return false;

                return true;
            }

            private bool TryGetTryStaticCallParameters (
                out ParameterDefinition rpcIdParamDef,
                out ParameterDefinition parametersPayloadSizeParamDef,
                out ParameterDefinition rpcBufferPositionParamDef)
            {
                rpcIdParamDef = null;
                parametersPayloadSizeParamDef = null;
                rpcBufferPositionParamDef = null;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out rpcIdParamDef))
                    return false;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(ilProcessor.Body.Method, out parametersPayloadSizeParamDef))
                    return false;

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(ilProcessor.Body.Method, out rpcBufferPositionParamDef))
                    return false;

                return true;
            }

            private bool TryGetExecuteQueuedRPCMethodILProcessor (
                TypeReference rpcInterfacesTypeRef,
                RPCExecutionStage rpcExecutionStage,
                out ILProcessor ilProcessor)
            {
                if (cachedExecuteQueuedRPCMethodILProcessors != null && cachedExecuteQueuedRPCMethodILProcessors.TryGetValue(rpcExecutionStage, out ilProcessor))
                    return true;

                Type markerType = null;
                switch (rpcExecutionStage)
                {
                    case RPCExecutionStage.BeforeFixedUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCBeforeFixedUpdateMarker);
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCAfterFixedUpdateMarker);
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCBeforeUpdateMarker);
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCAfterUpdateMarker);
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCBeforeLateUpdateMarker);
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        markerType = typeof(RPCInterfaceRegistry.ExecuteRPCAfterLateUpdateMarker);
                        break;

                    case RPCExecutionStage.Automatic:
                    case RPCExecutionStage.ImmediatelyOnArrival:
                    default:
                        Debug.LogError($"Invalid {nameof(RPCExecutionStage)} to queue RPC: \"{rpcExecutionStage}\".");
                        ilProcessor = null;
                        return false;
                }

                if (!TryFindMethodReferenceWithAttribute(
                    rpcInterfacesTypeRef.Resolve(),
                    rpcInterfacesTypeRef.Module.ImportReference(markerType),
                    out var methodRef))
                {
                    ilProcessor = null;
                    return false;
                }

                var methodDef = methodRef.Resolve();
                methodDef.Body.Instructions.Clear();
                ilProcessor = methodDef.Body.GetILProcessor();

                ilProcessor.Emit(OpCodes.Nop);
                ilProcessor.Emit(OpCodes.Ret);

                cachedExecuteQueuedRPCMethodILProcessors.Add(rpcExecutionStage, ilProcessor);

                return true;
            }

            private bool TryGetQueueMethodReference (
                RPCExecutionStage rpcExecutionStage,
                out MethodReference methodRef)
            {
                var rpcInterfaceRegistryRef = moduleDef.ImportReference(typeof(RPCInterfaceRegistry));

                switch (rpcExecutionStage)
                {
                    case RPCExecutionStage.BeforeFixedUpdate:
                        TryFindMethodReferenceWithAttribute(
                                rpcInterfaceRegistryRef.Resolve(), 
                                moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeFixedUpdateRPCMarker)), 
                                out methodRef);
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        TryFindMethodReferenceWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterFixedUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        TryFindMethodReferenceWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        TryFindMethodReferenceWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        TryFindMethodReferenceWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(), 
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeLateUpdateRPCMarker)), 
                            out methodRef);
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        TryFindMethodReferenceWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(), 
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterLateUpdateRPCMarker)), 
                            out methodRef);
                        break;

                    case RPCExecutionStage.ImmediatelyOnArrival:
                    case RPCExecutionStage.Automatic:
                    default:
                        methodRef = null;
                        return false;
                }

                return true;
            }

            private bool TryInjectQueueCall (
                RPCExecutionStage rpcExecutionStage,
                ParameterDefinition rpcBufferPositionParamDef,
                ParameterDefinition parametersPayloadSizeParamDef,
                ref Instruction afterInstruction)
            {
                if (!TryGetQueueMethodReference(
                    rpcExecutionStage,
                    out var queueRPCMethodRef))
                    return false;

                InsertCallAfter(ilProcessor, ref afterInstruction, queueRPCMethodRef);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Add);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Conv_U2);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Stind_I2);
                InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);

                return true;
            }

            private bool TryInjectQueueStaticRPCCall (
                Instruction beforeInstruction,
                RPCExecutionStage rpcExecutionStage,
                out Instruction firstInstruction)
            {
                var newInstruction = Instruction.Create(OpCodes.Nop);
                ilProcessor.InsertBefore(beforeInstruction, newInstruction);
                var afterInstruction = firstInstruction = newInstruction;

                if (!TryGetTryStaticCallParameters(
                    out var rpcIdParamDef,
                    out var parametersPayloadSizeParamDef,
                    out var rpcBufferPositionParamDef))
                    return false;

                // Pipe ID = 0 when the method we want to call is static.
                InsertPushIntAfter(ilProcessor, ref afterInstruction, 0);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);

                TryInjectQueueCall(
                    rpcExecutionStage,
                    rpcBufferPositionParamDef,
                    parametersPayloadSizeParamDef,
                    ref afterInstruction);

                return true;
            }

            private bool TryInjectQueueInstanceRPCCall (
                Instruction beforeInstruction,
                RPCExecutionStage rpcExecutionStage,
                out Instruction firstInstruction)
            {
                var afterInstruction = firstInstruction = InsertBefore(ilProcessor, beforeInstruction, OpCodes.Nop);

                if (!TryGetTryCallParameters(
                    out var pipeIdParamDef,
                    out var rpcIdParamDef,
                    out var parametersPayloadSizeParamDef,
                    out var rpcBufferPositionParamDef))
                    return false;

                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, pipeIdParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);

                TryInjectQueueCall(
                    rpcExecutionStage,
                    rpcBufferPositionParamDef,
                    parametersPayloadSizeParamDef,
                    ref afterInstruction);

                return true;
            }

            private bool TryInjectSwitchCaseForRPC (
                ILProcessor ilProcessor,
                Instruction afterInstruction,
                int valueToPushForBeq,
                Instruction jmpToInstruction,
                out Instruction lastInstructionOfSwitchJmp)
            {
                if (afterInstruction == null)
                {
                    Debug.LogError("Unable to inject switch jump instructions, the instruction we want to inject AFTER is null!");
                    lastInstructionOfSwitchJmp = null;
                    return false;
                }

                if (jmpToInstruction == null)
                {
                    Debug.LogError("Unable to inject switch jump instructions, the target instruction to jump to is null!");
                    lastInstructionOfSwitchJmp = null;
                    return false;
                }

                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out var rpcIdParamDef))
                {
                    lastInstructionOfSwitchJmp = null;
                    return false;
                }

                InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: false);

                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldc_I4, valueToPushForBeq);
                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Beq, jmpToInstruction);
                lastInstructionOfSwitchJmp = afterInstruction;

                return true;
            }

            public bool TryInjectSwitchCaseForImmediateRPCExecutionMethod ()
            {
                var afterInstruction = lastSwitchJmpInstruction;
                if (afterInstruction == null)
                {
                    Debug.LogError("Unable to inject default switch return instructions, the instruction we want to inject after is null!");
                    return false;
                }

                if (lastInstruction == null)
                {
                    Debug.LogError("Unable to inject default switch return instructions, the failure instruction that we want to jump to is null!");
                    return false;
                }

                InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Br, lastInstruction);

                return true;
            }

            private bool TryInjectILToExecuteQueuedRPC(
                MethodReference targetMethod,
                RPCExecutionStage rpcExecutionStage,
                int rpcId)
            {
                if (!TryGetExecuteQueuedRPCMethodILProcessor(
                    ilProcessor.Body.Method.DeclaringType,
                    rpcExecutionStage,
                    out var executeQueuedRPCMethodILProcessor))
                    return false;

                var firstExecuteQueuedRPCMethodInstruction = executeQueuedRPCMethodILProcessor.Body.Instructions[0];

                Instruction lastExecuteQueuedRPCSwitchJmpInstruction = null;
                if (executionStageLastSwitchJmpInstructions == null || !executionStageLastSwitchJmpInstructions.TryGetValue(rpcExecutionStage, out lastExecuteQueuedRPCSwitchJmpInstruction))
                {
                    lastExecuteQueuedRPCSwitchJmpInstruction = firstExecuteQueuedRPCMethodInstruction;
                    executionStageLastSwitchJmpInstructions.Add(rpcExecutionStage, lastExecuteQueuedRPCSwitchJmpInstruction);
                }

                var lastExecuteQueuedRPCSwitchInstruction = executeQueuedRPCMethodILProcessor.Body.Instructions[executeQueuedRPCMethodILProcessor.Body.Instructions.Count - 1];

                Instruction firstInstructionOfExecuteQueuedRPCMethod;
                var targetMethodDef = targetMethod.Resolve();
                if (targetMethodDef.IsStatic)
                {
                     if (!TryInjectStaticRPCExecution(
                        executeQueuedRPCMethodILProcessor,
                        beforeInstruction: lastExecuteQueuedRPCSwitchInstruction,
                        targetMethodRef: targetMethod,
                        isImmediateRPCExeuction: false,
                        firstInstructionOfInjection: out firstInstructionOfExecuteQueuedRPCMethod))
                        return false;
                }

                else if (!TryInjectInstanceRPCExecution(
                    executeQueuedRPCMethodILProcessor,
                    beforeInstruction: lastExecuteQueuedRPCSwitchInstruction,
                    targetMethod: targetMethod,
                    isImmediateRPCExeuction: false,
                    firstInstructionOfInjection: out firstInstructionOfExecuteQueuedRPCMethod))
                    return false;

                if (!TryInjectSwitchCaseForRPC(
                    executeQueuedRPCMethodILProcessor,
                    afterInstruction: lastExecuteQueuedRPCSwitchJmpInstruction,
                    valueToPushForBeq: rpcId,
                    jmpToInstruction: firstInstructionOfExecuteQueuedRPCMethod,
                    lastInstructionOfSwitchJmp: out lastExecuteQueuedRPCSwitchJmpInstruction))
                    return false;

                executionStageLastSwitchJmpInstructions[rpcExecutionStage] = lastExecuteQueuedRPCSwitchJmpInstruction;

                return true;
            }

            private static readonly RPCExecutionStage[] queuedRPCExecutionStages = new RPCExecutionStage[6]
            {
                RPCExecutionStage.BeforeFixedUpdate,
                RPCExecutionStage.AfterFixedUpdate,
                RPCExecutionStage.BeforeUpdate,
                RPCExecutionStage.AfterUpdate,
                RPCExecutionStage.BeforeLateUpdate,
                RPCExecutionStage.AfterLateUpdate
            };

            private bool TryInjectILForQueuedRPC(
                MethodReference targetMethod,
                RPCExecutionStage rpcExecutionStage,
                int rpcId)
            {
                if (rpcExecutionStage == RPCExecutionStage.Automatic)
                {
                    for (int i = 0; i < queuedRPCExecutionStages.Length; i++)
                        if (!TryInjectILToExecuteQueuedRPC(
                            targetMethod,
                            queuedRPCExecutionStages[i],
                            rpcId))
                            return false;

                    return true;
                }

                return TryInjectILToExecuteQueuedRPC(targetMethod, rpcExecutionStage, rpcId);
            }

            private bool TryDetermineRPCExecutionStageAutomatically (
                MethodReference targetMethodRef,
                out RPCExecutionStage rpcExecutionStage)
            {
                var rpcExecutionStageTypeRef = targetMethodRef.Module.ImportReference(typeof(RPCExecutionStage));
                rpcExecutionStage = RPCExecutionStage.Automatic;

                if (!cachedCallTree.TryGetValue(targetMethodRef.MetadataToken, out var call))
                    goto missingMethodFailure;

                if (call.callingMethods == null || call.callingMethods.Count == 0)
                {
                    Debug.LogError($"Method: \"{targetMethodRef.Name}\" declared in type: \"{targetMethodRef.DeclaringType.Name}\" is flagged for determing it's RPC execution stage automatically. However, we cannot determine where this method is called from.");
                    return false;
                }

                List<MetadataToken> currentCallers = new List<MetadataToken>() { targetMethodRef.MetadataToken };
                List<MethodReference> rootCallers = new List<MethodReference>();

                while (true)
                {
                    List<MetadataToken> nextCallers = new List<MetadataToken>();
                    foreach (var callMetadataToken in currentCallers)
                    {
                        if (!cachedCallTree.TryGetValue(callMetadataToken, out call))
                            goto missingMethodFailure;

                        foreach (var callerMetadataToken in call.callingMethods)
                        {
                            if (!cachedCallTree.TryGetValue(callerMetadataToken, out var caller))
                                goto missingMethodFailure;

                            if (caller.callingMethods.Count == 0)
                                rootCallers.Add(caller.calledMethodRef);
                            else nextCallers.Add(callerMetadataToken);
                        }
                    }

                    currentCallers = nextCallers;
                    if (currentCallers.Count == 0)
                        break;
                }

                var distinctRootCallers = rootCallers.Distinct();
                if (distinctRootCallers.Count() > 1)
                {
                    string stringOfRootCallers = rootCallers.Select(rc => rc.Name).Aggregate((aggregation, methodName) => $"{aggregation}\n\t{methodName},");
                    Debug.LogError($"Method: \"{targetMethodRef.Name}\" declared in type: \"{targetMethodRef.DeclaringType.Name}\" is flagged for determing it's RPC execution stage automatically. However, it's currently being called from multiple independent roots which are: {stringOfRootCallers}\n We will fall back to RPC execution stage: \"{RPCExecutionStage.ImmediatelyOnArrival}\".");
                    return false;
                }

                var rootCaller = rootCallers[0];

                TypeReference monoBehaviourType = targetMethodRef.Module.ImportReference(typeof(MonoBehaviour));
                TypeReference declaringType = rootCaller.DeclaringType;
                var rootCallerMethodDef = rootCaller.Resolve();

                if (MethodIsCoroutine(rootCallerMethodDef))
                {
                    rpcExecutionStage = RPCExecutionStage.AfterUpdate;
                    return true;
                }

                while (declaringType != null)
                {
                    if (declaringType.MetadataToken == monoBehaviourType.MetadataToken)
                        break;

                    declaringType = declaringType.DeclaringType;
                }

                if (declaringType == null)
                {
                    Debug.LogError($"Method: \"{targetMethodRef.Name}\" declared in type: \"{targetMethodRef.DeclaringType.Name}\" is flagged for determing it's RPC execution stage automatically. However, the root calling type: \"{rootCaller.DeclaringType.Name}\" does not derrive from MonoBehaviour.");
                    return false;
                }

                string rootCallerSignature = $"{declaringType.Namespace}.{nameof(MonoBehaviour)}.{rootCaller.Name}";
                if (!cachedMonoBehaviourMethodSignaturesForRPCExecutionStages.TryGetValue(rootCallerSignature, out rpcExecutionStage))
                {
                    Debug.LogError($"Method: \"{targetMethodRef.Name}\" declared in type: \"{targetMethodRef.DeclaringType.Name}\" is flagged for determing it's RPC execution stage automatically. However, the root caller: \"{rootCaller.Name}\" declared in type: \"{rootCaller.DeclaringType.Name}\" is not a supported MonoBehaviour method to automatically determine the RPC execution stage.");
                    return false;
                }

                return true;

                missingMethodFailure:
                Debug.LogError($"Unable to append RPC execution stage method argument, the call tree is invalid!");
                return false;
            }

            public bool ProcessMethodDef (
                int rpcId,
                MethodReference targetMethodRef,
                RPCExecutionStage rpcExecutionStage)
            {
                Instruction firstInstructionOfSwitchCaseImpl = null;
                var targetMethodDef = targetMethodRef.Resolve();

                if (!TryInjectRPCInterceptIL(
                    rpcId,
                    (int)rpcExecutionStage,
                    targetMethodRef))
                    goto unableToInjectIL;

                if (targetMethodDef.IsStatic)
                {
                    if (!TryInjectStaticRPCExecution(
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: targetMethodRef,
                        isImmediateRPCExeuction: true,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                        goto unableToInjectIL;
                }

                else if (!TryInjectInstanceRPCExecution(
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    targetMethod: targetMethodRef,
                    isImmediateRPCExeuction: true,
                    firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                    goto unableToInjectIL;

                if (rpcExecutionStage != RPCExecutionStage.ImmediatelyOnArrival)
                    if (!TryInjectILForQueuedRPC(
                        targetMethodRef,
                        rpcExecutionStage,
                        rpcId))
                        goto unableToInjectIL;

                if (firstInstructionOfSwitchCaseImpl != null && !TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    afterInstruction: lastSwitchJmpInstruction,
                    valueToPushForBeq: rpcId,
                    jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                    lastInstructionOfSwitchJmp: out lastSwitchJmpInstruction))
                    goto unableToInjectIL;

                // Debug.Log($"Injected RPC intercept assembly into method: \"{targetRPCMethodDef.Name}\" in class: \"{targetRPCMethodDef.DeclaringType.FullName}\".");
                return true;

                unableToInjectIL:
                Debug.LogError($"Failure occurred while attempting to post process method: \"{targetMethodRef.Name}\" in class: \"{targetMethodRef.DeclaringType.FullName}\".");
                goto cleanup;

                cleanup:
                ilProcessor.Body.Instructions.Clear();
                return false;
            }
        }
    }
}
