using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    internal partial class RPCILPostProcessor
    {
        sealed class RPCILGenerator
        {
            public const string GeneratedRPCILNamespace = "Unity.ClusterDisplay.Generated";
            public const string GeneratedRPCILTypeName = "RPCIL";

            private AssemblyDefinition compiledAssemblyDef;
            private ModuleDefinition moduleDef;
            private ILProcessor ilProcessor;

            private Instruction firstInstruction, lastInstruction, lastSwitchJmpInstruction;
            private TypeReference generatedRPCILTypeRef;

            public RPCILGenerator (TypeReference generatedRPCILTypeRef)
            {
                this.compiledAssemblyDef = generatedRPCILTypeRef.Module.Assembly;
                this.moduleDef = generatedRPCILTypeRef.Module;
                this.generatedRPCILTypeRef = generatedRPCILTypeRef;
            }

            public bool TrySetup (Type methodAttributeMarker)
            {
                CodeGenDebug.Log($"Setting up {nameof(RPCILGenerator)}.");

                if (!GetRPCInstanceRegistryMethodImplementation(
                    compiledAssemblyDef,
                    methodAttributeMarker, 
                    out ilProcessor))
                    return false;

                firstInstruction = ilProcessor.Body.Instructions[0];
                lastInstruction = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
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
                        type = typeof(RPCInterfaceRegistry.QueueAfterFixedUpdateRPCMarker);
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

            public void InjectDefaultSwitchCase ()
            {
                if (lastSwitchJmpInstruction == null)
                    return;

                var instructionToJmpTo = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                var newInstruction = Instruction.Create(OpCodes.Br, instructionToJmpTo);
                ilProcessor.InsertAfter(lastSwitchJmpInstruction, newInstruction);
                CodeGenDebug.Log($"Inserting last switch statement: \"{lastSwitchJmpInstruction.ToString()}");
            }

            public bool TryAppendInstanceRPCExecution (MethodDefinition targetMethodDef, string rpcHash)
            {
                var importedTargetMethodRef = CecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);
                Instruction firstInstructionOfSwitchCaseImpl = null;

                bool success =
                    TryInjectInstanceRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        executionTarget: importedTargetMethodRef,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl) &&
                    TryInjectSwitchCaseForRPC(
                        ilProcessor,
                        valueToPushForBeq: rpcHash,
                        jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                        afterInstruction: ref lastSwitchJmpInstruction);

                if (!success)
                {
                    goto unableToInjectIL;
                }

                return true;

                unableToInjectIL:
                CodeGenDebug.LogError($"Failure occurred while attempting to append instance method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }

            public bool TryAppendStaticRPCExecution (MethodDefinition targetMethodDef, string rpcHash)
            {
                var importedTargetMethodRef = CecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);
                Instruction firstInstructionOfSwitchCaseImpl = null;

                bool success =
                    TryInjectStaticRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: importedTargetMethodRef,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl) &&
                    TryInjectSwitchCaseForRPC(
                        ilProcessor,
                        valueToPushForBeq: rpcHash,
                        jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                        afterInstruction: ref lastSwitchJmpInstruction);

                CodeGenDebug.Log($"Appended: \"{lastSwitchJmpInstruction.ToString()}");

                if (!success)
                {
                    goto unableToInjectIL;
                }

                return true;

                unableToInjectIL:
                CodeGenDebug.LogError($"Failure occurred while attempting to append static method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }
        }
    }
}
