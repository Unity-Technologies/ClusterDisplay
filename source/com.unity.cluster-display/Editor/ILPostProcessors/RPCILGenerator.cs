using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        sealed class RPCILGenerator
        {
            public const string GeneratedRPCILNamespace = "Unity.ClusterDisplay.Generated";
            public const string GeneratedRPCILTypeName = "RPCIL";

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

                firstInstruction = ilProcessor.Body.Instructions[0];
                // Last instruction is two instructions before end of method since we are returning false by default.
                lastInstruction = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                lastSwitchJmpInstruction = ilProcessor.Body.Instructions.LastOrDefault(instruction => instruction.OpCode == OpCodes.Beq);
                if (lastSwitchJmpInstruction == null)
                    lastSwitchJmpInstruction = firstInstruction;
                return true;
            }

            private bool GetRPCInstanceRegistryMethodImplementation (
                AssemblyDefinition assemblyDef, 
                Type markerAttribute, 
                out ILProcessor il)
            {
                if (!CecilUtils.TryImport(assemblyDef.MainModule, markerAttribute, out var onTryCallMarkerAttributeTypeRef))
                {
                    il = null;
                    return false;
                }

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
                if (!CecilUtils.TryImport(moduleDef, typeof(RPCInterfaceRegistry), out var rpcInterfaceRegistryRef))
                {
                    methodRef = null;
                    return false;
                }

                Type type = null;
                switch (rpcExecutionStage)
                {
                    case RPCExecutionStage.BeforeFixedUpdate:
                        type = typeof(RPCInterfaceRegistry.QueueBeforeFixedUpdateRPCMarker);
                        break;

                    case RPCExecutionStage.AfterFixedUpdate:
                        break;

                    case RPCExecutionStage.BeforeUpdate:
                        type = typeof(RPCInterfaceRegistry.QueueBeforeUpdateRPCMarker);
                        break;

                    case RPCExecutionStage.AfterUpdate:
                        type = typeof(RPCInterfaceRegistry.QueueAfterUpdateRPCMarker);
                        break;

                    case RPCExecutionStage.BeforeLateUpdate:
                        type = typeof(RPCInterfaceRegistry.QueueBeforeLateUpdateRPCMarker);
                        break;

                    case RPCExecutionStage.AfterLateUpdate:
                        type = typeof(RPCInterfaceRegistry.QueueAfterLateUpdateRPCMarker);
                        break;

                    case RPCExecutionStage.ImmediatelyOnArrival:
                    case RPCExecutionStage.Automatic:
                    default:
                        methodRef = null;
                        return false;
                }

                if (!CecilUtils.TryImport(moduleDef, type, out var typeRef))
                {
                    methodRef = null;
                    return false;
                }

                if (!TryFindMethodReferenceWithAttributeInModule(
                    rpcInterfaceRegistryRef.Module,
                    rpcInterfaceRegistryRef.Resolve(),
                    typeRef,
                    out methodRef))
                    return false;

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

            public void InjectDefaultSwitchCase ()
            {
                if (lastSwitchJmpInstruction == null)
                    return;

                var isntructionToJmpTo = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                var newInstruction = Instruction.Create(OpCodes.Br, isntructionToJmpTo);
                ilProcessor.InsertAfter(lastSwitchJmpInstruction, newInstruction);
            }

            public bool TryAppendInstanceRPCExecution (MethodDefinition targetMethodDef, ushort rpcId)
            {
                Instruction firstInstructionOfSwitchCaseImpl = null;
                var importedTargetMethodRef = CecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);

                if (!TryInjectInstanceRPCExecution(
                    generatedRPCILTypeRef.Module,
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    executionTarget: importedTargetMethodRef,
                    isImmediateRPCExeuction: true,
                    firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                    goto unableToInjectIL;

                if (!TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    afterInstruction: lastSwitchJmpInstruction,
                    valueToPushForBeq: rpcId,
                    jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                    lastInstructionOfSwitchJmp: out lastSwitchJmpInstruction))
                    return false;

                return true;

                unableToInjectIL:
                LogWriter.LogError($"Failure occurred while attempting to append instance method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }

            public bool TryAppendStaticRPCExecution (MethodDefinition targetMethodDef, ushort rpcId)
            {
                Instruction firstInstructionOfSwitchCaseImpl = null;
                var importedTargetMethodRef = CecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);

                if (!TryInjectStaticRPCExecution(
                    generatedRPCILTypeRef.Module,
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    targetMethodRef: importedTargetMethodRef,
                    isImmediateRPCExeuction: true,
                    firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl))
                    goto unableToInjectIL;

                if (!TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    afterInstruction: lastSwitchJmpInstruction,
                    valueToPushForBeq: rpcId,
                    jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                    lastInstructionOfSwitchJmp: out lastSwitchJmpInstruction))
                    return false;

                return true;

                unableToInjectIL:
                LogWriter.LogError($"Failure occurred while attempting to append static method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }
        }
    }
}
