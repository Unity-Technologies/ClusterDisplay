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

            RPCILPostProcessor rpcILPostProcessor;
            AssemblyDefinition compiledAssemblyDef;
            ModuleDefinition moduleDef;
            ILProcessor ilProcessor;

            Instruction firstInstruction, lastInstruction, lastSwitchJmpInstruction;
            TypeReference generatedRPCILTypeRef;

            public RPCILGenerator (RPCILPostProcessor rpcILPostProcessor, TypeReference generatedRPCILTypeRef)
            {
                this.rpcILPostProcessor = rpcILPostProcessor;
                this.compiledAssemblyDef = generatedRPCILTypeRef.Module.Assembly;
                this.moduleDef = generatedRPCILTypeRef.Module;
                this.generatedRPCILTypeRef = generatedRPCILTypeRef;
            }

            public bool TrySetup (Type methodAttributeMarker)
            {
                rpcILPostProcessor.logger.Log($"Setting up {nameof(RPCILGenerator)}.");

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

            bool GetRPCInstanceRegistryMethodImplementation (
                AssemblyDefinition assemblyDef, 
                Type markerAttribute, 
                out ILProcessor il)
            {
                if (!rpcILPostProcessor.cecilUtils.TryImport(assemblyDef.MainModule, markerAttribute, out var onTryCallMarkerAttributeTypeRef))
                {
                    il = null;
                    return false;
                }

                if (!rpcILPostProcessor.TryFindMethodReferenceWithAttributeInModule(
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

            bool TryGetQueueMethodReference (
                RPCExecutionStage rpcExecutionStage,
                out MethodReference methodRef)
            {
                if (!rpcILPostProcessor.cecilUtils.TryImport(moduleDef, typeof(RPCInterfaceRegistry), out var rpcInterfaceRegistryRef))
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

                if (!rpcILPostProcessor.cecilUtils.TryImport(moduleDef, type, out var typeRef))
                {
                    methodRef = null;
                    return false;
                }

                if (!rpcILPostProcessor.TryFindMethodReferenceWithAttributeInModule(
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
                rpcILPostProcessor.logger.Log($"Inserting last switch statement: \"{lastSwitchJmpInstruction.ToString()}");
            }

            public bool TryAppendInstanceRPCExecution (MethodDefinition targetMethodDef, string rpcHash)
            {
                var importedTargetMethodRef = rpcILPostProcessor.cecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);
                Instruction firstInstructionOfSwitchCaseImpl = null;

                bool success =
                    rpcILPostProcessor.TryInjectInstanceRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        executionTarget: importedTargetMethodRef,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl) &&
                    rpcILPostProcessor.TryInjectSwitchCaseForRPC(
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
                rpcILPostProcessor.logger.LogError($"Failure occurred while attempting to append instance method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }

            public bool TryAppendStaticRPCExecution (MethodDefinition targetMethodDef, string rpcHash)
            {
                var importedTargetMethodRef = rpcILPostProcessor.cecilUtils.Import(generatedRPCILTypeRef.Module, targetMethodDef);
                Instruction firstInstructionOfSwitchCaseImpl = null;

                bool success =
                    rpcILPostProcessor.TryInjectStaticRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: importedTargetMethodRef,
                        firstInstructionOfInjection: out firstInstructionOfSwitchCaseImpl) &&
                    rpcILPostProcessor.TryInjectSwitchCaseForRPC(
                        ilProcessor,
                        valueToPushForBeq: rpcHash,
                        jmpToInstruction: firstInstructionOfSwitchCaseImpl,
                        afterInstruction: ref lastSwitchJmpInstruction);

                rpcILPostProcessor.logger.Log($"Appended: \"{lastSwitchJmpInstruction.ToString()}");

                if (!success)
                {
                    goto unableToInjectIL;
                }

                return true;

                unableToInjectIL:
                rpcILPostProcessor.logger.LogError($"Failure occurred while attempting to append static method execution to method: \"{ilProcessor.Body.Method.Name}\" declared in type: \"{ilProcessor.Body.Method.DeclaringType.Name}\".");
                ilProcessor.Body.Instructions.Clear();
                return false;
            }
        }
    }
}
