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
        public override ILPostProcessor GetInstance() => this;

        private bool TryInjectBridgeToRPCPropagation (
            AssemblyDefinition assemblyDef, 
            ushort rpcId, 
            MethodDefinition targetMethodDef)
        {
            var beforeInstruction = targetMethodDef.Body.Instructions.First();
            var il = targetMethodDef.Body.GetILProcessor();
            var rpcEmitterType = typeof(RPCEmitter);

            var rpcEmitterTypeReference = assemblyDef.MainModule.ImportReference(rpcEmitterType);

            MethodInfo appendRPCMethodInfo = null;
            if (!targetMethodDef.IsStatic)
            {
                if (!TryFindMethodWithAttribute<RPCEmitter.RPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                    return false;
            }

            else if (!TryFindMethodWithAttribute<RPCEmitter.StaticRPCCallMarker>(rpcEmitterType, out appendRPCMethodInfo))
                return false;

            if (!TryFindMethodWithAttribute<RPCEmitter.CopyValueToBufferMarker>(rpcEmitterType, out var copyValueToBufferMethodInfo))
                return false;

            var openRPCLatchMethodRef = assemblyDef.MainModule.ImportReference(appendRPCMethodInfo);
            var copyValueToBufferMethodRef = assemblyDef.MainModule.ImportReference(copyValueToBufferMethodInfo);

            var parameters = targetMethodDef.Parameters;
            ushort sizeOfAllParameters = 0;
            foreach (var param in parameters)
            {
                var typeReference = assemblyDef.MainModule.ImportReference(param.ParameterType);

                int sizeOfType = 0;
                if (!TryDetermineSizeOfType(typeReference.Resolve(), ref sizeOfType))
                    return false;

                if (sizeOfType > ushort.MaxValue || ((int)sizeOfAllParameters) + sizeOfType > ushort.MaxValue)
                    return false;

                sizeOfAllParameters += (ushort)sizeOfType;
            }

            Instruction newInstruction = null;
            Instruction previousInstruction = null;

            if (!TryGetCachedGetIsMasterMarkerMethod(out var getIsMasterMethod))
                return false;

            newInstruction = Instruction.Create(OpCodes.Call, assemblyDef.MainModule.ImportReference(getIsMasterMethod));
            il.InsertBefore(beforeInstruction, newInstruction);
            previousInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Brfalse_S, beforeInstruction);
            il.InsertAfter(previousInstruction, newInstruction);
            previousInstruction = newInstruction;

            InjectOpenRPCLatchCall(
                il, 
                previousInstruction, 
                targetMethodDef.IsStatic, 
                rpcId, 
                openRPCLatchMethodRef, 
                sizeOfAllParameters,
                out previousInstruction);

            foreach (var param in parameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(copyValueToBufferMethodRef);
                genericInstanceMethod.GenericArguments.Add(param.ParameterType);

                newInstruction = PushParameterToStack(param, param.IsOut || param.IsIn);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethod);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));

            return true;
        }

        private bool TryGetTryCallParameters (
            MethodDefinition methodDef,
            out ParameterDefinition pipeIdParamDef,
            out ParameterDefinition rpcIdParamDef,
            out ParameterDefinition parametersPayloadSizeParamDef,
            out ParameterDefinition rpcBufferPositionParamDef)
        {
            pipeIdParamDef = null;
            rpcIdParamDef = null;
            parametersPayloadSizeParamDef = null;
            rpcBufferPositionParamDef = null;

            if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(methodDef, out pipeIdParamDef))
                return false;

            if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(methodDef, out rpcIdParamDef))
                return false;

            if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(methodDef, out parametersPayloadSizeParamDef))
                return false;

            if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(methodDef, out rpcBufferPositionParamDef))
                return false;

            return true;
        }

        private bool TryGetQueueMethodReference (
            ModuleDefinition moduleDef,
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

        private bool TryInjectQueueOnArrival (
            ModuleDefinition moduleDef,
            ILProcessor il,
            Instruction injectBeforeInstruction,
            RPCExecutionStage rpcExecutionStage,
            out Instruction firstInstruction)
        {
            var newInstruction = Instruction.Create(OpCodes.Nop);
            il.InsertBefore(injectBeforeInstruction, newInstruction);
            var afterInstruction = firstInstruction = newInstruction;

            if (!TryGetTryCallParameters(
                il.Body.Method,
                out var pipeIdParamDef,
                out var rpcIdParamDef,
                out var parametersPayloadSizeParamDef,
                out var rpcBufferPositionParamDef))
                return false;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S,  pipeIdParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcIdParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, parametersPayloadSizeParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldind_U2);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            if (!TryGetQueueMethodReference(
                moduleDef,
                rpcExecutionStage,
                out var queueRPCMethodRef))
                return false;

            newInstruction = Instruction.Create(OpCodes.Call, queueRPCMethodRef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, rpcBufferPositionParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldind_U2);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldarg_S, parametersPayloadSizeParamDef);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Add);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Conv_U2);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Stind_I2);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4_1);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ret);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            return true;
        }

        private bool InjectRPCExecution (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            Instruction injectBeforeInstruction,
            MethodReference objectRegistryGetItemMethodRef,
            MethodReference targetMethodToExecute,
            bool isImmediateRPCExeuction,
            out Instruction firstInstructionOfInjection)
        {
            if (!TryFindMethodWithAttribute<RPCEmitter.ParseStructureMarker>(typeof(RPCEmitter), out var parseStructureMethod) ||
                !TryFindParameterWithAttribute<RPCInterfaceRegistry.ObjectRegistryMarker>(ilProcessor.Body.Method, out var objectParamDef) ||
                !TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(ilProcessor.Body.Method, out var pipeIdParamDef) ||
                !TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(ilProcessor.Body.Method, out var bufferPosParamDef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            Instruction newInstruction = null;

            /*
            firstInstructionOfInjection = Instruction.Create(OpCodes.Nop);
            ilProcessor.InsertBefore(injectBeforeInstruction, firstInstructionOfInjection);
            var afterInstruction = firstInstructionOfInjection;
            */

            // InsertDebugMessage(moduleDef, ilProcessor, "HELLO", afterInstruction, out afterInstruction);

            /*
            newInstruction = PushParameterToStack(objectParamDef, false);
            ilProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;
            */

            firstInstructionOfInjection = PushParameterToStack(objectParamDef, false);
            ilProcessor.InsertBefore(injectBeforeInstruction, firstInstructionOfInjection);
            var afterInstruction = firstInstructionOfInjection;

            newInstruction = PushParameterToStack(pipeIdParamDef, false); // Load pipeId parameter onto stack.
            ilProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            // Call objectRegistry[pipeId].
            newInstruction = Instruction.Create(OpCodes.Callvirt, objectRegistryGetItemMethodRef);
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

             // Import the ParseStructureMethod method.
            var parseStructureMethodRef = moduleDef.ImportReference(parseStructureMethod);

            // Loop through all parameters of the method we want to call on our object.
            foreach (var parameterReference in targetMethodToExecute.Parameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(parseStructureMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                var paramRef = moduleDef.ImportReference(parameterReference.ParameterType);
                paramRef.IsValueType = true;
                genericInstanceMethod.GenericArguments.Add(paramRef);
                var genericInstanceMethodRef = moduleDef.ImportReference(genericInstanceMethod);

                newInstruction = PushParameterToStack(bufferPosParamDef, !isImmediateRPCExeuction);
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethodRef); // Call generic method to convert bytes into our struct.
                ilProcessor.InsertAfter(afterInstruction, newInstruction);
                afterInstruction = newInstruction;
            }

            newInstruction = Instruction.Create(OpCodes.Callvirt, targetMethodToExecute); // Call our method on the target object with all the parameters.
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

        private bool GetOnTryCallILProcessor (AssemblyDefinition assemblyDef, out TypeReference derrivedTypeRef, out ILProcessor il)
        {
            var rpcInterfaceRegistryType = typeof(RPCInterfaceRegistry);
            var onTryCallMarkerAttributeType = typeof(RPCInterfaceRegistry.OnTryCallMarker);

            var rpcInstanceRegistryTypeDef = assemblyDef.MainModule.ImportReference(rpcInterfaceRegistryType).Resolve();
            var onTryCallMarkerAttributeTypeRef = assemblyDef.MainModule.ImportReference(onTryCallMarkerAttributeType);

            var derrivedTypeDef = assemblyDef.MainModule.GetTypes()
                .Where(typeDef => 
                    typeDef != null && 
                    typeDef.BaseType != null && 
                    typeDef.BaseType.FullName == rpcInstanceRegistryTypeDef.FullName).FirstOrDefault();

            if (derrivedTypeDef == null)
            {
                derrivedTypeRef = null;
                il = null;
                return false;
            }

            // if (!TryFindMethodWithAttribute<RPCInterfaceRegistry.OnTryCallMarker>(rpcInterfaceRegistryType, out var onTryCallMethodInfo))
            if (!TryFindMethodDefinitionWithAttribute(derrivedTypeDef, onTryCallMarkerAttributeTypeRef, out var onTryCallMethodDef))
            {
                derrivedTypeRef = null;
                il = null;
                return false;
            }

            derrivedTypeRef = onTryCallMethodDef.DeclaringType;
            il = onTryCallMethodDef.Body.GetILProcessor();

            /*
            var onTryCallMethodDef = assemblyDef.MainModule.ImportReference(onTryCallMethodInfo);
            derrivedTypeRef = onTryCallMethodDef.DeclaringType;
            il = onTryCallMethodDef.Resolve().Body.GetILProcessor();
            */

            return true;
        }

        private bool TryImportTryGetSingletonObject<T> (
            ModuleDefinition moduleDefinition,
            out TypeReference typeRef,
            out MethodReference tryGetInstanceMethodRef)
        {
            var type = typeof(T);
            typeRef = moduleDefinition.ImportReference(type);

            if (typeRef == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var typeDef = typeRef.Resolve();
            if (type.BaseType == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseTypeRef = moduleDefinition.ImportReference(type.BaseType);
            if (baseTypeRef == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericType = new GenericInstanceType(baseTypeRef);
            baseGenericType.GenericArguments.Add(typeRef);

            if (!TryFindMethodWithAttribute<SingletonScriptableObjectTryGetInstanceMarker>(type.BaseType, out var tryGetInstanceMethod))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericMethodRef = moduleDefinition.ImportReference(tryGetInstanceMethod);

            return (tryGetInstanceMethodRef = baseGenericMethodRef) != null;
        }

        private bool TryGetObjectRegistryGetItemMethodRef (
            ModuleDefinition moduleDefinition,
            out MethodReference objectRegistryGetItemMethodRef)
        {
            if (!TryFindPropertyGetMethodWithAttribute<ObjectRegistry.ObjectRegistryGetItemMarker>(typeof(ObjectRegistry), out var methodInfo))
            {
                objectRegistryGetItemMethodRef = null;
                return false;
            }

            return (objectRegistryGetItemMethodRef = moduleDefinition.ImportReference(methodInfo)) != null;
        }

        private bool InjectSwitchJmp (
            ILProcessor il,
            Instruction afterInstruction,
            ushort valueToPushForBeq,
            Instruction jmpToInstruction,
            out Instruction lastInstructionOfSwitchJmp)
        {
            if (!TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(il.Body.Method, out var rpcIdParamDef))
            {
                lastInstructionOfSwitchJmp = null;
                return false;
            }

            var newInstruction = PushParameterToStack(rpcIdParamDef, false);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4, valueToPushForBeq);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Beq, jmpToInstruction);
            il.InsertAfter(afterInstruction, newInstruction);
            lastInstructionOfSwitchJmp = newInstruction;

            return true;
        }

        private static bool InjectDefaultSwitchReturn (
            ILProcessor il,
            Instruction afterInstruction,
            Instruction failureInstructionToJumpTo,
            out Instruction lastInstructionOfSwitchJmp)
        {
            var newInstruction = Instruction.Create(OpCodes.Br_S, failureInstructionToJumpTo);
            il.InsertAfter(afterInstruction, newInstruction);
            lastInstructionOfSwitchJmp = newInstruction;

            return true;
        }

        private void InsertDebugMessage (
            ModuleDefinition moduleDef,
            ILProcessor il,
            string message,
            Instruction afterInstruction,
            out Instruction lastInstruction)
        {
            var debugType = typeof(Debug);
            var debugTypeRef = moduleDef.ImportReference(debugType);
            var debugTypeDef = debugTypeRef.Resolve();
            var logMethodRef = moduleDef.ImportReference(debugTypeDef.Methods.Where(method => {
                return
                    method.Name == "Log" &&
                    method.Parameters.Count == 1;
            }).FirstOrDefault());

            lastInstruction = null;

            var newInstruction = Instruction.Create(OpCodes.Ldstr, message);
            il.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Call, logMethodRef);
            il.InsertAfter(afterInstruction, newInstruction);
            lastInstruction = newInstruction;
        }

        private void InjectObjectRegistryTryGet(
            ModuleDefinition moduleDef,
            ILProcessor onTryCallILProcessor,
            TypeDefinition objectRegistryTypeDef,
            MethodReference objectRegistryTryGetInstance,
            Instruction afterInstruction,
            out Instruction tryGetInstanceFailureInstruction,
            out Instruction lastInstruction)
        {
            var objectRegistryLocalVariable = new VariableDefinition(moduleDef.ImportReference(objectRegistryTypeDef));
            onTryCallILProcessor.Body.Variables.Add(objectRegistryLocalVariable);

            var newInstruction = Instruction.Create(OpCodes.Ldloca_S,  onTryCallILProcessor.Body.Variables[0]);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Ldc_I4_0);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            newInstruction = Instruction.Create(OpCodes.Call, objectRegistryTryGetInstance);
            onTryCallILProcessor.InsertAfter(afterInstruction, newInstruction);
            afterInstruction = newInstruction;

            tryGetInstanceFailureInstruction = Instruction.Create(OpCodes.Brfalse, onTryCallILProcessor.Body.Instructions[0]);
            onTryCallILProcessor.InsertAfter(afterInstruction, tryGetInstanceFailureInstruction);
            lastInstruction = tryGetInstanceFailureInstruction;
        }

        private bool TryGetCachedGetIsMasterMarkerMethod (out MethodInfo getIsMasterMethod)
        {
            if (cachedGetIsMasterMethod == null && !TryFindPropertyGetMethodWithAttribute<ClusterDisplayState.IsMasterMarker>(typeof(ClusterDisplayState), out cachedGetIsMasterMethod))
            {
                getIsMasterMethod = null;
                return false;
            }

            getIsMasterMethod = cachedGetIsMasterMethod;
            return true;
        }

        private bool TryGetCachedDebugLogMethodReference (ModuleDefinition moduleDef, out MethodReference methodReference)
        {
            if (cachedDebugLogMethodRef == null)
            {
                moduleDef.ImportReference(typeof(Debug));
                cachedDebugLogMethodRef = moduleDef.ImportReference(typeof(Debug).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(method => method.Name == "Log"));
            }

            return (methodReference = cachedDebugLogMethodRef) != null;
        }

        private bool TrySetup (
            ICompiledAssembly compiledAssembly, 
            out AssemblyDefinition assemblyDef,
            out SerializedRPC[] serializedRPCs)
        {
            assemblyDef = null;
            serializedRPCs = null;

            if (compiledAssembly.Name != ReflectionUtils.DefaultUserAssemblyName)
                return false;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out assemblyDef))
                return false;

            return true;
        }

        private bool TrySetupOnTryCall (
            AssemblyDefinition assemblyDef, 
            out ILProcessor onTryCallILProcessor, 
            out TypeReference rpcInterfacesTypeRef,
            out ModuleDefinition rpcInterfacesModule, 
            out Instruction firstInstruction,
            out Instruction beginningOfFailureInstruction)
        {
            if (!GetOnTryCallILProcessor(assemblyDef, out rpcInterfacesTypeRef, out onTryCallILProcessor))
            {
                rpcInterfacesModule = null;
                firstInstruction = null;
                beginningOfFailureInstruction = null;
                return false;
            }

            rpcInterfacesModule = rpcInterfacesTypeRef.Module;

            onTryCallILProcessor.Body.Instructions.Clear();
            onTryCallILProcessor.Body.Variables.Clear();
            onTryCallILProcessor.Body.InitLocals = false;

            firstInstruction = Instruction.Create(OpCodes.Nop);
            onTryCallILProcessor.Append(firstInstruction);

            beginningOfFailureInstruction = Instruction.Create(OpCodes.Ldc_I4_0);
            onTryCallILProcessor.Append(beginningOfFailureInstruction);
            onTryCallILProcessor.Append(Instruction.Create(OpCodes.Ret));
            return true;
        }

        private bool TryInjectDebugLogMessage (ModuleDefinition moduleDef, ILProcessor ilProcessor, Instruction afterInstruction, out Instruction lastInstruction)
        {
            if (!TryGetCachedDebugLogMethodReference(moduleDef, out var debugLogMethodRef))
            {
                lastInstruction = afterInstruction;
                return false;
            }

            lastInstruction = Instruction.Create(OpCodes.Ldstr, "HELLO!");
            ilProcessor.InsertAfter(afterInstruction, lastInstruction);

            lastInstruction = Instruction.Create(OpCodes.Call, debugLogMethodRef);
            ilProcessor.InsertAfter(afterInstruction, lastInstruction);
            return true;
        }

        private bool TryGetExecuteQueuedRPCMethodILProcessor (
            ModuleDefinition rpcInterfacesModuleDef,
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
                rpcInterfacesModuleDef.ImportReference(markerType),
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

        private void ProcessMethodDef (
            AssemblyDefinition compiledAssemblyDef,
            ModuleDefinition rpcInterfacesModuleDef,
            TypeReference rpcInterfacesTypeRef,
            ILProcessor onTryCallILProcessor,
            Instruction firstOfOnTryFailureInstructions,
            ref Instruction lastSwitchJmpOfOnTryCallMethod,
            MethodReference objectRegistryTryGetItemMethodRef,
            ushort rpcId,
            MethodDefinition targetRPCMethodDef,
            RPCExecutionStage rpcExecutionStage)
        {
            if (!TryInjectBridgeToRPCPropagation(
                compiledAssemblyDef,
                rpcId,
                targetRPCMethodDef))
                return;

            Instruction firstInstructionOfOnTryCallSwitchCase = null;
            var serializedRPCExecutionStage = rpcExecutionStage;
            if (serializedRPCExecutionStage == RPCExecutionStage.ImmediatelyOnArrival)
            {
                InjectRPCExecution(
                    rpcInterfacesModuleDef,
                    ilProcessor: onTryCallILProcessor,
                    injectBeforeInstruction: firstOfOnTryFailureInstructions,
                    objectRegistryTryGetItemMethodRef,
                    targetMethodToExecute: targetRPCMethodDef,
                    isImmediateRPCExeuction: true,
                    firstInstructionOfInjection: out firstInstructionOfOnTryCallSwitchCase);
            }

            else
            {
                if (TryGetExecuteQueuedRPCMethodILProcessor(
                    rpcInterfacesModuleDef,
                    rpcInterfacesTypeRef,
                    serializedRPCExecutionStage,
                    out var executeQueuedRPCMethodILProcessor))
                {
                    if (!TryInjectQueueOnArrival(
                        rpcInterfacesModuleDef,
                        onTryCallILProcessor,
                        injectBeforeInstruction: firstOfOnTryFailureInstructions,
                        serializedRPCExecutionStage,
                        firstInstruction: out firstInstructionOfOnTryCallSwitchCase))
                        return;

                    var firstExecuteQueuedRPCMethodInstruction = executeQueuedRPCMethodILProcessor.Body.Instructions[0];

                    Instruction lastExecuteQueuedRPCSwitchJmpInstruction = null;
                    if (lastSwitchJmpInstruction == null || !lastSwitchJmpInstruction.TryGetValue(serializedRPCExecutionStage, out lastExecuteQueuedRPCSwitchJmpInstruction))
                    {
                        lastExecuteQueuedRPCSwitchJmpInstruction = firstExecuteQueuedRPCMethodInstruction;
                        if (lastSwitchJmpInstruction == null)
                            lastSwitchJmpInstruction = new Dictionary<RPCExecutionStage, Instruction>() { { serializedRPCExecutionStage, lastExecuteQueuedRPCSwitchJmpInstruction } };
                        else lastSwitchJmpInstruction.Add(serializedRPCExecutionStage, lastExecuteQueuedRPCSwitchJmpInstruction);
                    }

                    var lastExecuteQueuedRPCSwitchInstruction = executeQueuedRPCMethodILProcessor.Body.Instructions[executeQueuedRPCMethodILProcessor.Body.Instructions.Count - 1];

                    InjectRPCExecution(
                        rpcInterfacesModuleDef,
                        executeQueuedRPCMethodILProcessor,
                        injectBeforeInstruction: lastExecuteQueuedRPCSwitchInstruction,
                        objectRegistryTryGetItemMethodRef,
                        targetMethodToExecute: targetRPCMethodDef,
                        isImmediateRPCExeuction: false,
                        firstInstructionOfInjection: out var firstInstructionOfExecuteQueuedRPCMethod);

                    InjectSwitchJmp(
                        executeQueuedRPCMethodILProcessor,
                        afterInstruction: lastExecuteQueuedRPCSwitchJmpInstruction,
                        valueToPushForBeq: rpcId,
                        jmpToInstruction: firstInstructionOfExecuteQueuedRPCMethod,
                        lastInstructionOfSwitchJmp: out lastExecuteQueuedRPCSwitchJmpInstruction);

                    lastSwitchJmpInstruction[serializedRPCExecutionStage] = lastExecuteQueuedRPCSwitchJmpInstruction;
                }
            }

            InjectSwitchJmp(
                onTryCallILProcessor,
                afterInstruction: lastSwitchJmpOfOnTryCallMethod,
                valueToPushForBeq: rpcId,
                jmpToInstruction: firstInstructionOfOnTryCallSwitchCase,
                lastInstructionOfSwitchJmp: out lastSwitchJmpOfOnTryCallMethod);

            Debug.Log($"Injected RPC intercept assembly into method: \"{targetRPCMethodDef.Name}\" in class: \"{targetRPCMethodDef.DeclaringType.FullName}\".");
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!TrySetup(
                compiledAssembly,
                out var compiledAssemblyDef,
                out var serializedRPCs))
                return null;

            if (!TrySetupOnTryCall(
                compiledAssemblyDef,
                out var onTryCallILProcessor,
                out var rpcInterfacesTypeRef,
                out var rpcInterfacesModuleDef,
                out var firstOnTryInstruction,
                out var firstOfOnTryFailureInstructions))
                return null;

            if (!TryGetObjectRegistryGetItemMethodRef(
                rpcInterfacesModuleDef,
                out var objectRegistryTryGetItemMethodRef))
                return null;

            Instruction lastSwitchJmpOfOnTryCallMethod = null;
            lastSwitchJmpOfOnTryCallMethod = firstOnTryInstruction;

            List<ushort> usedRPCIds = new List<ushort>();
            if (RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out serializedRPCs) && serializedRPCs.Length > 0)
            {
                foreach (var serializedRPC in serializedRPCs)
                {
                    var rpc = serializedRPC;
                    var typeDefinition = compiledAssemblyDef.MainModule.GetType(rpc.declaryingTypeFullName);

                    if (!TryGetMethodDefinition(typeDefinition, ref rpc, out var targetRPCMethodDef))
                    {
                        Debug.LogError($"Unable to find method signature: \"{rpc.methodName}\".");
                        continue;
                    }

                    Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

                    ProcessMethodDef(
                        compiledAssemblyDef,
                        rpcInterfacesModuleDef,
                        rpcInterfacesTypeRef,
                        onTryCallILProcessor,
                        firstOfOnTryFailureInstructions,
                        ref lastSwitchJmpOfOnTryCallMethod,
                        objectRegistryTryGetItemMethodRef,
                        rpc.rpcId,
                        targetRPCMethodDef,
                        (RPCExecutionStage)rpc.rpcExecutionStage);

                    usedRPCIds.Add(rpc.rpcId);
                }
            }

            Queue<ushort> unusedRPCIds = new Queue<ushort>();
            int lastRPCId = -1;

            if (usedRPCIds.Count > 0)
            {
                usedRPCIds.Sort();
                lastRPCId = usedRPCIds.Last();
                for (ushort rpcId = 0; rpcId < lastRPCId; rpcId++)
                {
                    if (usedRPCIds.Contains(rpcId))
                        continue;
                    unusedRPCIds.Enqueue(rpcId);
                }
            }

            var rpcMethodCustomAttributeType = typeof(RPCMethod);
            var rpcMethodCustomAttributeTypeRef = rpcInterfacesModuleDef.ImportReference(rpcMethodCustomAttributeType);
            string rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;
            var rpcMethodAttributeRPCExecutionStageArgument = rpcInterfacesModuleDef.ImportReference(typeof(RPCExecutionStage));
            var methodDefs = compiledAssemblyDef.Modules
                .SelectMany(moduleDef => moduleDef.Types
                    .SelectMany(type => type.Methods
                        .Where(method => method.CustomAttributes
                            .Any(customAttribute => 
                            customAttribute.HasConstructorArguments &&
                            customAttribute.AttributeType.FullName == rpcMethodAttributeFullName))));

            foreach (var targetRPCMethodDef in methodDefs)
            {
                Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

                var customAttribute = targetRPCMethodDef.CustomAttributes.First(ca => ca.AttributeType.FullName == rpcMethodAttributeFullName);
                if (!TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPCMethod.RPCExecutionStageMarker>(customAttribute, out var rpcExecutionStageAttributeArgumentIndex) ||
                    !TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPCMethod.RPCIDMarker>(customAttribute, out var rpcIdAttributeArgumentIndex))
                    continue;

                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[rpcExecutionStageAttributeArgumentIndex];

                ushort newRPCId = unusedRPCIds.Count > 0 ? unusedRPCIds.Dequeue() : (ushort)++lastRPCId;
                ProcessMethodDef(
                    compiledAssemblyDef,
                    rpcInterfacesModuleDef,
                    rpcInterfacesTypeRef,
                    onTryCallILProcessor,
                    firstOfOnTryFailureInstructions,
                    ref lastSwitchJmpOfOnTryCallMethod,
                    objectRegistryTryGetItemMethodRef,
                    newRPCId,
                    targetRPCMethodDef,
                    (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value);

                var customAttributeArgument = customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex];
                customAttribute.ConstructorArguments.RemoveAt(rpcIdAttributeArgumentIndex);
                customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(customAttributeArgument.Type, newRPCId));
            }

            InjectDefaultSwitchReturn(
                onTryCallILProcessor,
                afterInstruction: lastSwitchJmpOfOnTryCallMethod,
                failureInstructionToJumpTo: firstOfOnTryFailureInstructions,
                out var _);

            if (cachedExecuteQueuedRPCMethodILProcessors != null)
            {
                foreach (var cachedExecutedRPCMethodILProcessor in cachedExecuteQueuedRPCMethodILProcessors)
                {
                    if (!lastSwitchJmpInstruction.TryGetValue(cachedExecutedRPCMethodILProcessor.Key, out var lastExecuteQueuedRPCJmpInstruction))
                        continue;

                    InjectDefaultSwitchReturn(
                        cachedExecutedRPCMethodILProcessor.Value,
                        afterInstruction: lastExecuteQueuedRPCJmpInstruction,
                        failureInstructionToJumpTo: cachedExecutedRPCMethodILProcessor.Value.Body.Instructions[cachedExecutedRPCMethodILProcessor.Value.Body.Instructions.Count - 2],
                        out var _);

                }
            }

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            try
            {
                compiledAssemblyDef.Write(pe, writerParameters);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
