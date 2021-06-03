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

                lastInstruction = Instruction.Create(OpCodes.Ldc_I4_0);
                ilProcessor.Append(lastInstruction);
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));

                lastSwitchJmpInstruction = firstInstruction;
                return true;
            }

            private bool TryGetObjectRegistryGetItemMethodRef (out MethodReference objectRegistryGetItemMethodRef)
            {
                if (cachedObjectRegistryGetItemMethodRef != null)
                {
                    objectRegistryGetItemMethodRef = cachedObjectRegistryGetItemMethodRef;
                    return true;
                }

                if (!TryFindPropertyGetMethodWithAttribute<ObjectRegistry.ObjectRegistryGetItemMarker>(typeof(ObjectRegistry), out var methodInfo))
                {
                    objectRegistryGetItemMethodRef = null;
                    return false;
                }

                return (objectRegistryGetItemMethodRef = cachedObjectRegistryGetItemMethodRef = moduleDef.ImportReference(methodInfo)) != null;
            }

            private bool TryInjectRPCInterceptIL (
                ushort rpcId, 
                MethodDefinition targetMethodDef)
            {
                var beforeInstruction = targetMethodDef.Body.Instructions.First();
                var il = targetMethodDef.Body.GetILProcessor();

                var rpcEmitterType = typeof(RPCEmitter);
                var rpcEmitterTypeReference = targetMethodDef.Module.ImportReference(rpcEmitterType);

                MethodInfo appendRPCMethodInfo = null;
                if (!targetMethodDef.IsStatic)
                {
                    if (!TryFindMethodWithAttribute<RPCEmitter.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                        return false;
                }

                else if (!TryFindMethodWithAttribute<RPCEmitter.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;

                var appendRPCCMethodRef = targetMethodDef.Module.ImportReference(appendRPCMethodInfo);

                if (!TryGetCachedGetIsMasterMarkerMethod(out var getIsMasterMethod))
                    return false;

                if (!TryPollParameterInformation(
                    targetMethodDef.Module,
                    targetMethodDef,
                    out var totalSizeOfStaticallySizedRPCParameters,
                    out var hasDynamicallySizedRPCParameters))
                    return false;

                var newInstruction = Instruction.Create(OpCodes.Call, targetMethodDef.Module.ImportReference(getIsMasterMethod));
                il.InsertBefore(beforeInstruction, newInstruction);
                var afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Brfalse_S, beforeInstruction);
                il.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                return
                    !hasDynamicallySizedRPCParameters ?

                        TryInjectBridgeToStaticallySizedRPCPropagation(
                            rpcEmitterType,
                            rpcId,
                            targetMethodDef,
                            il,
                            afterInstruction,
                            appendRPCCMethodRef,
                            totalSizeOfStaticallySizedRPCParameters)

                        :

                        TryInjectBridgeToDynamicallySizedRPCPropagation(
                            rpcEmitterType,
                            rpcId,
                            targetMethodDef,
                            il,
                            afterInstruction,
                            appendRPCCMethodRef,
                            totalSizeOfStaticallySizedRPCParameters);
            }

            private bool InjectPushOfRPCParamters (
                ILProcessor ilProcessor,
                MethodDefinition targetMethodToExecute,
                ParameterDefinition bufferPosParamDef,
                bool isImmediateRPCExeuction,
                ref Instruction afterInstruction)
            {
                Instruction newInstruction = null;
                foreach (var paramDef in targetMethodToExecute.Parameters)
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

                        newInstruction = PushParameterToStack(bufferPosParamDef, isStatic: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;

                        newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethodRef); // Call generic method to convert bytes into our struct.
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;
                    }

                    else if (ParameterIsString(targetMethodToExecute.Module, paramDef))
                    {
                        if (!TryFindMethodWithAttribute<RPCEmitter.ParseStringMarker>(typeof(RPCEmitter), out var parseStringMethod))
                            return false;

                        var parseStringMethodRef = moduleDef.ImportReference(parseStringMethod);

                        newInstruction = PushParameterToStack(bufferPosParamDef, isStatic: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;

                        newInstruction = Instruction.Create(OpCodes.Call, parseStringMethodRef);
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;
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

                        newInstruction = PushParameterToStack(bufferPosParamDef, isStatic: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;

                        newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethod);
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;
                    }
                }

                return true;
            }

            private bool TryInjectInstanceRPCExecution (
                ILProcessor ilProcessor,
                Instruction beforeInstruction,
                MethodDefinition targetMethodToExecute,
                bool isImmediateRPCExeuction,
                out Instruction firstInstructionOfInjection)
            {
                var method = ilProcessor.Body.Method;
                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.ObjectRegistryMarker>(method, out var objectParamDef) ||
                    !TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(method, out var pipeIdParamDef) ||
                    !TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                if (!TryGetObjectRegistryGetItemMethodRef(out var objectRegistryTryGetItemMethodRef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                firstInstructionOfInjection = PushParameterToStack(objectParamDef, isStatic: method.IsStatic, byReference: false);
                ilProcessor.InsertBefore(beforeInstruction, firstInstructionOfInjection);
                var afterInstruction = firstInstructionOfInjection;

                var newInstruction = PushParameterToStack(pipeIdParamDef, isStatic: method.IsStatic, byReference: false); // Load pipeId parameter onto stack.
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                // Call objectRegistry[pipeId].
                newInstruction = Instruction.Create(OpCodes.Callvirt, objectRegistryTryGetItemMethodRef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                if (!targetMethodToExecute.HasParameters)
                {
                    // Call method on target object without any parameters.
                    newInstruction = Instruction.Create(OpCodes.Callvirt, targetMethodToExecute);

                    ilProcessor.InsertAfter(afterInstruction, newInstruction);
                    afterInstruction = newInstruction;

                    if (isImmediateRPCExeuction)
                    {
                        newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;
                    }

                    newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
                    ilProcessor.InsertAfter(afterInstruction, newInstruction);
                    return true;
                }

                InjectPushOfRPCParamters(
                    ilProcessor,
                    targetMethodToExecute,
                     bufferPosParamDef,
                     isImmediateRPCExeuction,
                     ref afterInstruction);

                newInstruction = Instruction.Create(OpCodes.Callvirt, targetMethodToExecute);

                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                if (isImmediateRPCExeuction)
                {
                    newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
                    ilProcessor.InsertAfter(afterInstruction, newInstruction);
                    afterInstruction = newInstruction;
                }

                newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                return true;
            }

            private bool TryInjectStaticRPCExecution (
                ILProcessor ilProcessor,
                Instruction beforeInstruction,
                MethodDefinition targetMethodToExecute,
                bool isImmediateRPCExeuction,
                out Instruction firstInstructionOfInjection)
            {
                var method = ilProcessor.Body.Method;
                if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
                {
                    firstInstructionOfInjection = null;
                    return false;
                }

                Instruction newInstruction = null;
                Instruction afterInstruction = null;

                if (!targetMethodToExecute.HasParameters)
                {
                    // Call method on target object without any parameters.
                    firstInstructionOfInjection = Instruction.Create(OpCodes.Call, targetMethodToExecute);

                    ilProcessor.InsertAfter(beforeInstruction, firstInstructionOfInjection);
                    afterInstruction = newInstruction;

                    if (isImmediateRPCExeuction)
                    {
                        newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
                        ilProcessor.InsertAfter(afterInstruction, newInstruction);
                        afterInstruction = newInstruction;
                    }

                    newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
                    ilProcessor.InsertAfter(afterInstruction, newInstruction);
                    return true;
                }

                firstInstructionOfInjection = Instruction.Create(OpCodes.Nop);
                ilProcessor.InsertBefore(beforeInstruction, firstInstructionOfInjection);
                afterInstruction = firstInstructionOfInjection;

                InjectPushOfRPCParamters(
                    ilProcessor,
                    targetMethodToExecute,
                     bufferPosParamDef,
                     isImmediateRPCExeuction,
                     ref afterInstruction);

                newInstruction = Instruction.Create(OpCodes.Call, targetMethodToExecute);

                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                if (isImmediateRPCExeuction)
                {
                    newInstruction = Instruction.Create(OpCodes.Ldc_I4_1); // Return true.
                    ilProcessor.InsertAfter(afterInstruction, newInstruction);
                    afterInstruction = newInstruction;
                }

                newInstruction = Instruction.Create(OpCodes.Ret); // Return true.
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
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

                    case RPCExecutionStage.ImmediatelyOnArrival:
                    default:
                        ilProcessor = null;
                        return false;
                }

                if (!TryFindMethodDefinitionWithAttribute(
                    rpcInterfacesTypeRef.Resolve(),
                    rpcInterfacesTypeRef.Module.ImportReference(markerType),
                    out var methodDef))
                {
                    ilProcessor = null;
                    return false;
                }

                methodDef.Body.Instructions.Clear();
                ilProcessor = methodDef.Body.GetILProcessor();

                ilProcessor.Emit(OpCodes.Nop);
                ilProcessor.Emit(OpCodes.Ret);

                if (cachedExecuteQueuedRPCMethodILProcessors == null)
                    cachedExecuteQueuedRPCMethodILProcessors = new Dictionary<RPCExecutionStage, ILProcessor>() { { rpcExecutionStage, ilProcessor } };
                else cachedExecuteQueuedRPCMethodILProcessors.Add(rpcExecutionStage, ilProcessor);

                return true;
            }

            private bool TryGetQueueMethodReference (
                RPCExecutionStage rpcExecutionStage,
                out MethodReference methodRef)
            {
                var rpcInterfaceRegistryRef = moduleDef.ImportReference(typeof(RPCInterfaceRegistry));
                MethodDefinition methodDef = null;
                methodRef = null;

                switch (rpcExecutionStage)
                {
                    case RPCExecutionStage.BeforeFixedUpdate:
                        TryFindMethodDefinitionWithAttribute(
                                rpcInterfaceRegistryRef.Resolve(), 
                                moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeFixedUpdateRPCMarker)), 
                                out methodDef);
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        TryFindMethodDefinitionWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterFixedUpdateRPCMarker)),
                            out methodDef);
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        TryFindMethodDefinitionWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeUpdateRPCMarker)),
                            out methodDef);
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        TryFindMethodDefinitionWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterUpdateRPCMarker)),
                            out methodDef);
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        TryFindMethodDefinitionWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(), 
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeLateUpdateRPCMarker)), 
                            out methodDef);
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        TryFindMethodDefinitionWithAttribute(
                            rpcInterfaceRegistryRef.Resolve(), 
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterLateUpdateRPCMarker)), 
                            out methodDef);
                        break;

                    case RPCExecutionStage.ImmediatelyOnArrival:
                    default:
                        return false;
                }

                methodRef = moduleDef.ImportReference(methodDef);
                return true;
            }

            private bool TryInjectQueueRPC (
                Instruction beforeInstruction,
                RPCExecutionStage rpcExecutionStage,
                out Instruction firstInstruction)
            {
                var newInstruction = Instruction.Create(OpCodes.Nop);
                ilProcessor.InsertBefore(beforeInstruction, newInstruction);
                var afterInstruction = firstInstruction = newInstruction;

                if (!TryGetTryCallParameters(
                    out var pipeIdParamDef,
                    out var rpcIdParamDef,
                    out var parametersPayloadSizeParamDef,
                    out var rpcBufferPositionParamDef))
                    return false;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S,  pipeIdParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcIdParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, parametersPayloadSizeParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldind_U2);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                if (!TryGetQueueMethodReference(
                    rpcExecutionStage,
                    out var queueRPCMethodRef))
                    return false;

                newInstruction = Instruction.Create(OpCodes.Call, queueRPCMethodRef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldind_U2);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldarg_S, parametersPayloadSizeParamDef);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Add);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Conv_U2);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Stind_I2);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldc_I4_1);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ret);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                return true;
            }

            private bool TryInjectSwitchCaseForRPC (
                ILProcessor ilProcessor,
                Instruction afterInstruction,
                ushort valueToPushForBeq,
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

                var newInstruction = PushParameterToStack(rpcIdParamDef, isStatic: ilProcessor.Body.Method.IsStatic, byReference: false);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Ldc_I4, valueToPushForBeq);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Beq, jmpToInstruction);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                lastInstructionOfSwitchJmp = newInstruction;

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

                var newInstruction = Instruction.Create(OpCodes.Br, lastInstruction);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);

                return true;
            }

            private bool TryInjectILForQueuedRPC(
                MethodDefinition targetMethodToExecute,
                RPCExecutionStage rpcExecutionStage,
                ushort rpcId)
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
                    if (executionStageLastSwitchJmpInstructions == null)
                        executionStageLastSwitchJmpInstructions = new Dictionary<RPCExecutionStage, Instruction>() { { rpcExecutionStage, lastExecuteQueuedRPCSwitchJmpInstruction } };
                    else executionStageLastSwitchJmpInstructions.Add(rpcExecutionStage, lastExecuteQueuedRPCSwitchJmpInstruction);
                }

                var lastExecuteQueuedRPCSwitchInstruction = executeQueuedRPCMethodILProcessor.Body.Instructions[executeQueuedRPCMethodILProcessor.Body.Instructions.Count - 1];

                Instruction firstInstructionOfExecuteQueuedRPCMethod;
                if (!targetMethodToExecute.IsStatic)
                {
                     if (!TryInjectStaticRPCExecution(
                        executeQueuedRPCMethodILProcessor,
                        beforeInstruction: lastExecuteQueuedRPCSwitchInstruction,
                        targetMethodToExecute: targetMethodToExecute,
                        isImmediateRPCExeuction: false,
                        firstInstructionOfInjection: out firstInstructionOfExecuteQueuedRPCMethod))
                        return false;
                }

                else if (!TryInjectInstanceRPCExecution(
                    executeQueuedRPCMethodILProcessor,
                    beforeInstruction: lastExecuteQueuedRPCSwitchInstruction,
                    targetMethodToExecute: targetMethodToExecute,
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

            public bool ProcessMethodDef (
                ushort rpcId,
                MethodDefinition targetMethodToExecute,
                RPCExecutionStage rpcExecutionStage)
            {
                if (!TryInjectRPCInterceptIL(
                    rpcId,
                    targetMethodToExecute))
                    goto unableToInjectIL;

                Instruction firstInstructionOfSwitchCaseImpl = null;

                if (rpcExecutionStage == RPCExecutionStage.ImmediatelyOnArrival)
                {
                    if (targetMethodToExecute.IsStatic)
                    {
                        if (!TryInjectStaticRPCExecution(
                            ilProcessor,
                            beforeInstruction: lastInstruction,
                            targetMethodToExecute: targetMethodToExecute,
                            isImmediateRPCExeuction: true,
                            firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                            goto unableToInjectIL;
                    }

                    else if (!TryInjectInstanceRPCExecution(
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodToExecute: targetMethodToExecute,
                        isImmediateRPCExeuction: true,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                        goto unableToInjectIL;
                }

                else
                {
                    if (!TryInjectQueueRPC(
                        beforeInstruction: lastInstruction,
                        rpcExecutionStage,
                        firstInstruction: out firstInstructionOfSwitchCaseImpl))
                        goto unableToInjectIL;

                    if (!TryInjectILForQueuedRPC(
                        targetMethodToExecute,
                        rpcExecutionStage,
                        rpcId))
                        goto unableToInjectIL;
                }

                if (!TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    afterInstruction: lastSwitchJmpInstruction,
                    valueToPushForBeq: rpcId,
                    jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                    lastInstructionOfSwitchJmp: out lastSwitchJmpInstruction))
                    goto unableToInjectIL;

                // Debug.Log($"Injected RPC intercept assembly into method: \"{targetRPCMethodDef.Name}\" in class: \"{targetRPCMethodDef.DeclaringType.FullName}\".");
                return true;

                unableToInjectIL:
                Debug.LogError($"Failure occurred while attempting to post process method: \"{targetMethodToExecute.Name}\" in class: \"{targetMethodToExecute.DeclaringType.FullName}\".");
                goto cleanup;

                cleanup:
                ilProcessor.Clear();
                return false;
            }
        }
    }
}
