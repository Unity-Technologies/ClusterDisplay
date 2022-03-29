using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private sealed class QueuedRPCILGenerator
        {
            private ILProcessor ilProcessor;
            private Instruction lastSwitchCaseInstruction;
            private TypeReference generatedRPCILTypeRef;

            public QueuedRPCILGenerator (TypeReference generatedRPCILTypeRef)
            {
                this.generatedRPCILTypeRef = generatedRPCILTypeRef;
            }

            public bool TrySetup ()
            {
                if (!TryGetCachedExecuteQueuedRPCMethodILProcessor(
                    out var queuedILProcessor))
                    return false;
                return true;
            }

            private Dictionary<string, Instruction> firstExecutionInstruction = new Dictionary<string, Instruction>();
            public bool TryInjectILToExecuteQueuedRPC(
                MethodReference targetMethod,
                RPCExecutionStage rpcExecutionStage,
                string rpcHash)
            {
                if (lastSwitchCaseInstruction == null)
                    lastSwitchCaseInstruction = ilProcessor.Body.Instructions[0];
                var lastInstruction = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 1];
                
                if (rpcHash == "319249A669B53F2684CF79162F60AC96CBE2EFFA")
                    CodeGenDebug.Log("TEST");

                Instruction firstInstructionOfCaseImpl;
                var targetMethodDef = targetMethod.Resolve();
                if (targetMethodDef.IsStatic)
                {
                     if (!TryInjectStaticRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: targetMethod,
                        isImmediateRPCExeuction: false,
                        firstInstructionOfInjection: out firstInstructionOfCaseImpl))
                        return false;
                }

                else if (!TryInjectInstanceRPCExecution(
                    generatedRPCILTypeRef.Module,
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    executionTarget: targetMethod,
                    isImmediateRPCExeuction: false,
                    firstInstructionOfInjection: out firstInstructionOfCaseImpl))
                    return false;
                
                if (!TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    afterInstruction: lastSwitchCaseInstruction,
                    valueToPushForBeq: rpcHash,
                    jmpToInstruction: firstInstructionOfCaseImpl,
                    lastInstructionOfSwitchJmp: out lastSwitchCaseInstruction))
                    return false;

                return true;
            }

            private bool TryGetCachedExecuteQueuedRPCMethodILProcessor (
                out ILProcessor ilProcessor)
            {
                if (this.ilProcessor != null)
                {
                    ilProcessor = this.ilProcessor;
                    return true;
                }

                if (!CecilUtils.TryImport(generatedRPCILTypeRef.Module, typeof(RPCInterfaceRegistry.ExecuteQueuedRPC), out var executeQueuedRPCAttributeTypeRef))
                {
                    ilProcessor = null;
                    return false;
                }

                if (!TryFindMethodReferenceWithAttributeInModule(
                    generatedRPCILTypeRef.Module,
                    generatedRPCILTypeRef.Resolve(),
                    executeQueuedRPCAttributeTypeRef,
                    out var methodRef))
                {
                    ilProcessor = null;
                    return false;
                }

                var methodDef = methodRef.Resolve();
                ilProcessor = methodDef.Body.GetILProcessor();

                this.ilProcessor = ilProcessor;

                return true;
            }

            public bool InjectDefaultSwitchCase()
            {
                if (lastSwitchCaseInstruction == null)
                    return true;

                var isntructionToJmpTo = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                var newInstruction = Instruction.Create(OpCodes.Br, isntructionToJmpTo);
                ilProcessor.InsertAfter(lastSwitchCaseInstruction, newInstruction);
                return true;
            }
        }
    }
}
