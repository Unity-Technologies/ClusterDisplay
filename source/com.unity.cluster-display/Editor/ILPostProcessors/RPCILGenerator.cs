﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        sealed class RPCILGenerator
        {
            private AssemblyDefinition compiledAssemblyDef;
            private ModuleDefinition moduleDef;
            private ILProcessor ilProcessor;

            private Instruction firstInstruction;
            private Instruction lastInstruction;

            private Instruction lastSwitchJmpInstruction;

            private TypeReference generatedRPCILTypeRef;

            public RPCILGenerator (TypeReference generatedRPCILTypeRef)
            {
                this.compiledAssemblyDef = generatedRPCILTypeRef.Module.Assembly;
                this.moduleDef = generatedRPCILTypeRef.Module;
                this.generatedRPCILTypeRef = generatedRPCILTypeRef;
            }

            public bool TrySetup (Type methodAttributeMarker)
            {
                if (!GetRPCInstanceRegistryMethodImplementation(
                    compiledAssemblyDef,
                    methodAttributeMarker, 
                    out ilProcessor))
                    return false;

                /*
                ilProcessor.Body.Instructions.Clear();
                ilProcessor.Body.Variables.Clear();
                ilProcessor.Body.InitLocals = false;

                firstInstruction = Instruction.Create(OpCodes.Nop);
                ilProcessor.Append(firstInstruction);

                lastInstruction = CecilUtils.PushInt(0);
                ilProcessor.Append(lastInstruction);
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));
                */

                firstInstruction = ilProcessor.Body.Instructions[0];
                // Last instruction is two instructions before end of method since we are returning false by default.
                lastInstruction = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];

                lastSwitchJmpInstruction = firstInstruction;
                return true;
            }

            private bool GetRPCInstanceRegistryMethodImplementation (
                AssemblyDefinition assemblyDef, 
                Type markerAttribute, 
                out ILProcessor il)
            {
                var onTryCallMarkerAttributeTypeRef = assemblyDef.MainModule.ImportReference(markerAttribute);

                if (!TryFindMethodReferenceWithAttributeInModule(
                    generatedRPCILTypeRef.Module,
                    generatedRPCILTypeRef.Resolve(), 
                    onTryCallMarkerAttributeTypeRef, 
                    out var onTryCallMethodRef))
                {
                    il = null;
                    return false;
                }

                var methodDef = onTryCallMethodRef.Resolve();
                il = methodDef.Body.GetILProcessor();

                return true;
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
                    if (!CecilUtils.TryFindMethodWithAttribute<RPCEmitter.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                        return false;
                }

                else if (!CecilUtils.TryFindMethodWithAttribute<RPCEmitter.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
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

                var afterInstruction = CecilUtils.InsertCallBefore(il, beforeInstruction, targetMethodRef.Module.ImportReference(getIsMasterMethod));
                CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Brfalse, beforeInstruction);

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

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(ilProcessor.Body.Method, out pipeIdParamDef))
                    return false;

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out rpcIdParamDef))
                    return false;

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(ilProcessor.Body.Method, out parametersPayloadSizeParamDef))
                    return false;

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(ilProcessor.Body.Method, out rpcBufferPositionParamDef))
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

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out rpcIdParamDef))
                    return false;

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(ilProcessor.Body.Method, out parametersPayloadSizeParamDef))
                    return false;

                if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(ilProcessor.Body.Method, out rpcBufferPositionParamDef))
                    return false;

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
                        TryFindMethodReferenceWithAttributeInModule(
                                rpcInterfaceRegistryRef.Module,
                                rpcInterfaceRegistryRef.Resolve(), 
                                moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeFixedUpdateRPCMarker)), 
                                out methodRef);
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        TryFindMethodReferenceWithAttributeInModule(
                            rpcInterfaceRegistryRef.Module,
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterFixedUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        TryFindMethodReferenceWithAttributeInModule(
                            rpcInterfaceRegistryRef.Module,
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        TryFindMethodReferenceWithAttributeInModule(
                            rpcInterfaceRegistryRef.Module,
                            rpcInterfaceRegistryRef.Resolve(),
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueAfterUpdateRPCMarker)),
                            out methodRef);
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        TryFindMethodReferenceWithAttributeInModule(
                            rpcInterfaceRegistryRef.Module,
                            rpcInterfaceRegistryRef.Resolve(), 
                            moduleDef.ImportReference(typeof(RPCInterfaceRegistry.QueueBeforeLateUpdateRPCMarker)), 
                            out methodRef);
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        TryFindMethodReferenceWithAttributeInModule(
                            rpcInterfaceRegistryRef.Module,
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

                CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, queueRPCMethodRef);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Add);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Conv_U2);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Stind_I2);
                CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);

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
                CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 0);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);

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
                var afterInstruction = firstInstruction = CecilUtils.InsertBefore(ilProcessor, beforeInstruction, OpCodes.Nop);

                if (!TryGetTryCallParameters(
                    out var pipeIdParamDef,
                    out var rpcIdParamDef,
                    out var parametersPayloadSizeParamDef,
                    out var rpcBufferPositionParamDef))
                    return false;

                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, pipeIdParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, parametersPayloadSizeParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcBufferPositionParamDef, isStaticCaller: false, byReference: false);
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldind_U2);

                TryInjectQueueCall(
                    rpcExecutionStage,
                    rpcBufferPositionParamDef,
                    parametersPayloadSizeParamDef,
                    ref afterInstruction);

                return true;
            }

            public void InjectDefaultSwitchCase ()
            {
                var afterInstruction = lastSwitchJmpInstruction;
                if (afterInstruction == null)
                {
                    Debug.LogError("Unable to inject default switch return instructions, the instruction we want to inject after is null!");
                    return;
                }

                if (lastInstruction == null)
                {
                    Debug.LogError("Unable to inject default switch return instructions, the failure instruction that we want to jump to is null!");
                    return;
                }

                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Br, lastInstruction);
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
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: targetMethodRef,
                        isImmediateRPCExeuction: true,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                        goto unableToInjectIL;
                }

                else if (!TryInjectInstanceRPCExecution(
                    generatedRPCILTypeRef.Module,
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    targetMethod: targetMethodRef,
                    isImmediateRPCExeuction: true,
                    firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
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
