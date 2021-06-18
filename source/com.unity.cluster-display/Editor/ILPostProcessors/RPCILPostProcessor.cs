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

        public bool TryInjectDefaultSwitchCaseForExecutionStageMethods()
        {
            if (cachedExecuteQueuedRPCMethodILProcessors != null)
            {
                foreach (var cachedExecutedRPCMethodILProcessor in cachedExecuteQueuedRPCMethodILProcessors)
                {
                    if (!executionStageLastSwitchJmpInstructions.TryGetValue(cachedExecutedRPCMethodILProcessor.Key, out var lastExecuteQueuedRPCJmpInstruction))
                        continue;

                    var isntructionToJmpTo = cachedExecutedRPCMethodILProcessor.Value.Body.Instructions[cachedExecutedRPCMethodILProcessor.Value.Body.Instructions.Count - 2];
                    var newInstruction = Instruction.Create(OpCodes.Br, isntructionToJmpTo);
                    cachedExecutedRPCMethodILProcessor.Value.InsertAfter(lastExecuteQueuedRPCJmpInstruction, newInstruction);
                }
            }

            return true;
        }

        private static void InsertCallAfter (ILProcessor il, ref Instruction afterInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertCallBefore (ILProcessor il, Instruction beforeInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void IsertPushLocalVariableAfter (ILProcessor il, ref Instruction afterInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction IsertPushLocalVariableBefore (ILProcessor il, Instruction beforeInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertPushParameterToStackAfter (ILProcessor il, ref Instruction afterInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertPushParameterToStackBefore (ILProcessor il, Instruction beforeInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertPushIntAfter (ILProcessor il, ref Instruction afterInstruction, int integer)
        {
            var instruction = PushInt(integer);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertPushIntBefore (ILProcessor il, Instruction beforeInstruction, int integer)
        {
            var instruction = PushInt(integer);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertPushStringAfter (ILProcessor il, ref Instruction afterInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertPushStringBefore (ILProcessor il, Instruction beforeInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertPushThisAfter (ILProcessor il, ref Instruction afterInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertPushThisBefore (ILProcessor il, Instruction beforeInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        private static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        private static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        /*
        private static void CachMethodReferencesInMethodInstructions (MethodDefinition callingMethodDef)
        {
            if (!cachedCallTree.TryGetValue(callingMethodDef.MetadataToken, out var call))
            {
                call = new Call
                {
                    callingMethods = new List<MetadataToken>(),
                    calledMethodRef = callingMethodDef,
                    methodsCalled = new List<MetadataToken>()
                };

                cachedCallTree.Add(callingMethodDef.MetadataToken, call);
            }

            for (int ii = 0; ii < callingMethodDef.Body.Instructions.Count; ii++)
            {
                var calledMethodDef = callingMethodDef.Body.Instructions[ii].Operand as MethodReference;
                if (calledMethodDef == null)
                    continue;

                if (!cachedCallTree.TryGetValue(calledMethodDef.MetadataToken, out var called))
                    cachedCallTree.Add(calledMethodDef.MetadataToken, new Call
                    {
                        callingMethods = new List<MetadataToken>() { callingMethodDef.MetadataToken },
                        calledMethodRef = calledMethodDef,
                        methodsCalled = new List<MetadataToken>()
                    });

                else called.callingMethods.Add(callingMethodDef.MetadataToken);

                call.methodsCalled.Add(calledMethodDef.MetadataToken);
            }
        }

        private static void CacheCallTree (ModuleDefinition moduleDef)
        {
            var coroutineTypeRef = moduleDef.ImportReference(typeof(System.Collections.IEnumerator));
            for (int ti = 0; ti < moduleDef.Types.Count; ti++)
            {
                for (int mi = 0; mi < moduleDef.Types[ti].Methods.Count; mi++)
                {
                    if (moduleDef.Types[ti].Methods[mi].Body == null)
                        continue;

                    var callingMethodDef = moduleDef.Types[ti].Methods[mi];

                    bool isCoroutine =
                        callingMethodDef.ReturnType.MetadataToken == coroutineTypeRef.MetadataToken &&
                        callingMethodDef.DeclaringType.HasNestedTypes;

                    if (isCoroutine)
                        for (int ni = 0; ni < callingMethodDef.DeclaringType.NestedTypes.Count; ni++)
                            for (int nmi = 0; nmi < callingMethodDef.DeclaringType.NestedTypes[ni].Methods.Count; nmi++)
                                CachMethodReferencesInMethodInstructions(callingMethodDef.DeclaringType.NestedTypes[ni].Methods[nmi]);

                    CachMethodReferencesInMethodInstructions(callingMethodDef);
                }
            }
        }
        */

        private static bool MethodIsCoroutine (MethodDefinition methodDef) => methodDef.ReturnType.MetadataToken == methodDef.Module.ImportReference(typeof(System.Collections.IEnumerator)).MetadataToken;

        private static bool TryPushMethodRef<DelegateMarker> (AssemblyDefinition compiledAssemblyDef, MethodReference methodRef, ILProcessor constructorILProcessor)
            where DelegateMarker : Attribute
        {
            constructorILProcessor.Emit(OpCodes.Ldnull);
            constructorILProcessor.Emit(OpCodes.Ldftn, methodRef);

            if (!TryFindNestedTypeWithAttribute<DelegateMarker>(compiledAssemblyDef.MainModule, typeof(RPCInterfaceRegistry), out var delegateTypeRef))
                return false;

            constructorILProcessor.Emit(OpCodes.Newobj, compiledAssemblyDef.MainModule.ImportReference(delegateTypeRef.Resolve().Methods.FirstOrDefault(method => method.IsConstructor)));
            return true;
        }

        private static bool TryCreateRPCILClassConstructor (
            AssemblyDefinition compiledAssemblyDef, 
            MethodReference onTryCallInstanceMethodDef,
            MethodReference onTryStaticCallInstanceMethodDef,
            MethodReference executeRPCBeforeFixedUpdateMethodDef,
            MethodReference executeRPCAfterFixedUpdateMethodDef,
            MethodReference executeRPCBeforeUpdateMethodDef,
            MethodReference executeRPCAfterUpdateMethodDef,
            MethodReference executeRPCBeforeLateUpdateMethodDef,
            MethodReference executeRPCAfterLateUpdateMethodDef,
            out MethodDefinition constructorMethodDef)
        {
            constructorMethodDef = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public, compiledAssemblyDef.MainModule.TypeSystem.Void);
            var constructorILProcessor = constructorMethodDef.Body.GetILProcessor();

            constructorILProcessor.Emit(OpCodes.Ldarg_0);

            if (!TryPushMethodRef<RPCInterfaceRegistry.OnTryCallDelegateMarker>(compiledAssemblyDef, onTryCallInstanceMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.OnTryStaticCallDelegateMarker>(compiledAssemblyDef, onTryStaticCallInstanceMethodDef, constructorILProcessor) || 
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCBeforeFixedUpdateMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCAfterFixedUpdateMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCBeforeUpdateMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCAfterUpdateMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCBeforeLateUpdateMethodDef, constructorILProcessor) ||
                !TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeRPCAfterLateUpdateMethodDef, constructorILProcessor))
            {
                constructorMethodDef = null;
                return false;
            }

            var constructorMethodInfo = typeof(RPCInterfaceRegistry).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(constructorInfo => constructorInfo.GetCustomAttribute<RPCInterfaceRegistry.RPCInterfaceRegistryConstuctorMarker>() != null);
            constructorILProcessor.Emit(OpCodes.Call, compiledAssemblyDef.MainModule.ImportReference(constructorMethodInfo));
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Ret);

            return true;
        }

        private static void AddCustomAttributeToParameterDef<Attribute> (AssemblyDefinition compiledAssemblyDef, ParameterDefinition parameterDef)
        {
            var onTryCallMarkerAttributeTypeDef = compiledAssemblyDef.MainModule.ImportReference(typeof(Attribute)).Resolve();
            var constructor = onTryCallMarkerAttributeTypeDef.Methods.FirstOrDefault(methodDef => methodDef.IsConstructor);
            parameterDef.CustomAttributes.Add(new CustomAttribute(compiledAssemblyDef.MainModule.ImportReference(constructor)));
        }

        private static void AddCustomAttribute<Attribute> (ModuleDefinition moduleDef, MethodDefinition methoDef)
        {
            var attributeTypeRef = moduleDef.ImportReference(typeof(Attribute)).Resolve();
            var constructor = attributeTypeRef.Methods.FirstOrDefault(method => method.IsConstructor);
            var customAttribute = new CustomAttribute(moduleDef.ImportReference(constructor));
            methoDef.CustomAttributes.Add(customAttribute);
        }

        private static bool TryCreateRPCILClass (AssemblyDefinition compiledAssemblyDef, out TypeReference rpcInterfaceRegistryDerrivedTypeRef)
        {
            var newTypeDef = new TypeDefinition("Unity.ClusterDisplay.Networking", "RPCIL", Mono.Cecil.TypeAttributes.Public);
            newTypeDef.BaseType = compiledAssemblyDef.MainModule.ImportReference(typeof(RPCInterfaceRegistry));

            var rpcIdParameterDef = new ParameterDefinition("rpcId", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            AddCustomAttributeToParameterDef<RPCInterfaceRegistry.RPCIdMarker>(compiledAssemblyDef, rpcIdParameterDef);

            var pipeParameterDef = new ParameterDefinition("pipeId", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            AddCustomAttributeToParameterDef<RPCInterfaceRegistry.PipeIdMarker>(compiledAssemblyDef, pipeParameterDef);

            var parametersPayloadSize = new ParameterDefinition("parametersPayloadSize", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            AddCustomAttributeToParameterDef<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(compiledAssemblyDef, parametersPayloadSize);

            var rpcBufferParameterPositionRef = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.In, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            var rpcBufferParameterPosition = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);

            AddCustomAttributeToParameterDef<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, rpcBufferParameterPositionRef);
            AddCustomAttributeToParameterDef<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, rpcBufferParameterPosition);

            var onTryCallInstanceMethodDef = new MethodDefinition("OnTryCallInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryCallInstanceMethodDef.Parameters.Add(rpcIdParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(pipeParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPositionRef);
            AddCustomAttribute<RPCInterfaceRegistry.OnTryCallMarker>(compiledAssemblyDef.MainModule, onTryCallInstanceMethodDef);

            var onTryStaticCallInstanceMethodDef = new MethodDefinition("OnTryStaticCallInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcIdParameterDef);
            onTryStaticCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPositionRef);
            AddCustomAttribute<RPCInterfaceRegistry.OnTryStaticCallMarker>(compiledAssemblyDef.MainModule, onTryStaticCallInstanceMethodDef);

            var executeRPCBeforeFixedUpdateMethodDef = new MethodDefinition("ExecuteRPCBeforeFixedUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCBeforeFixedUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCBeforeFixedUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCBeforeFixedUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCBeforeFixedUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCBeforeFixedUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCBeforeFixedUpdateMethodDef);

            var executeRPCAfterFixedUpdateMethodDef = new MethodDefinition("ExecuteRPCAfterFixedUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCAfterFixedUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCAfterFixedUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCAfterFixedUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCAfterFixedUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCAfterFixedUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCAfterFixedUpdateMethodDef);

            var executeRPCBeforeUpdateMethodDef = new MethodDefinition("ExecuteRPCBeforeUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCBeforeUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCBeforeUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCBeforeUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCBeforeUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCBeforeUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCBeforeUpdateMethodDef);

            var executeRPCAfterUpdateMethodDef = new MethodDefinition("ExecuteRPCAfterUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCAfterUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCAfterUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCAfterUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCAfterUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCAfterUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCAfterUpdateMethodDef);

            var executeRPCBeforeLateUpdateMethodDef = new MethodDefinition("ExecuteRPCBeforeLateUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCBeforeLateUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCBeforeLateUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCBeforeLateUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCBeforeLateUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCBeforeLateUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCBeforeLateUpdateMethodDef);

            var executeRPCAfterLateUpdateMethodDef = new MethodDefinition("ExecuteRPCAfterLateUpdate", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeRPCAfterLateUpdateMethodDef.Parameters.Add(rpcIdParameterDef);
            executeRPCAfterLateUpdateMethodDef.Parameters.Add(pipeParameterDef);
            executeRPCAfterLateUpdateMethodDef.Parameters.Add(parametersPayloadSize);
            executeRPCAfterLateUpdateMethodDef.Parameters.Add(rpcBufferParameterPosition);
            AddCustomAttribute<RPCInterfaceRegistry.ExecuteRPCAfterLateUpdateMarker>(compiledAssemblyDef.MainModule, executeRPCAfterLateUpdateMethodDef);

            newTypeDef.Methods.Add(onTryCallInstanceMethodDef);
            newTypeDef.Methods.Add(onTryStaticCallInstanceMethodDef);
            newTypeDef.Methods.Add(executeRPCBeforeFixedUpdateMethodDef);
            newTypeDef.Methods.Add(executeRPCAfterFixedUpdateMethodDef);
            newTypeDef.Methods.Add(executeRPCBeforeUpdateMethodDef);
            newTypeDef.Methods.Add(executeRPCAfterUpdateMethodDef);
            newTypeDef.Methods.Add(executeRPCBeforeLateUpdateMethodDef);
            newTypeDef.Methods.Add(executeRPCAfterLateUpdateMethodDef);

            if (!TryCreateRPCILClassConstructor(
                compiledAssemblyDef, 
                onTryCallInstanceMethodDef,
                onTryStaticCallInstanceMethodDef,
                executeRPCBeforeFixedUpdateMethodDef,
                executeRPCAfterFixedUpdateMethodDef,
                executeRPCBeforeUpdateMethodDef,
                executeRPCAfterUpdateMethodDef,
                executeRPCBeforeLateUpdateMethodDef,
                executeRPCAfterLateUpdateMethodDef,
                out var constructorMethodDef))
            {
                rpcInterfaceRegistryDerrivedTypeRef = null;
                return false;
            }

            newTypeDef.Methods.Add(constructorMethodDef);
            rpcInterfaceRegistryDerrivedTypeRef = newTypeDef;
            compiledAssemblyDef.MainModule.Types.Add(newTypeDef);
            return true;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name != ReflectionUtils.DefaultUserAssemblyName)
                goto ignoreAssembly;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out var compiledAssemblyDef))
                goto failure;

            var rpcInterfaceRegistryType = typeof(RPCInterfaceRegistry);
            var rpcInstanceRegistryTypeDef = compiledAssemblyDef.MainModule.ImportReference(rpcInterfaceRegistryType).Resolve();

            if (!TryCreateRPCILClass(compiledAssemblyDef, out var rpcInterfaceRegistryDerrivedTypeRef))
                goto failure;

            /*
            if (!TryGetDerrivedType(
                compiledAssemblyDef,
                rpcInstanceRegistryTypeDef, 
                out var rpcInterfaceRegistryDerrivedTypeRef))
                goto failure;
            */

            var onTryCallProcessor = new RPCExecutionILGenerator();
            if (!onTryCallProcessor.TrySetup(
                compiledAssemblyDef,
                rpcInterfaceRegistryDerrivedTypeRef,
                typeof(RPCInterfaceRegistry.OnTryCallMarker)))
                goto failure;

            var onTryStaticCallProcessor = new RPCExecutionILGenerator();
            if (!onTryStaticCallProcessor.TrySetup(
                compiledAssemblyDef,
                rpcInterfaceRegistryDerrivedTypeRef,
                typeof(RPCInterfaceRegistry.OnTryStaticCallMarker)))
                goto failure;

            // CacheCallTree(compiledAssemblyDef.MainModule);
            // CacheExecutionStageMethods();

            List<ushort> usedRPCIds = new List<ushort>();
            if (RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out var serializedRPCs) && serializedRPCs.Length > 0)
            {
                foreach (var serializedRPC in serializedRPCs)
                {
                    var rpc = serializedRPC;

                    var typeDefinition = compiledAssemblyDef.MainModule.GetType(rpc.declaryingTypeFullName);
                    MethodReference targetMethodRef = null;

                    if (!TryFindMethodWithMatchingFormalySerializedAs(
                        compiledAssemblyDef.MainModule,
                        typeDefinition,
                        rpc.methodName,
                        out targetMethodRef) &&
                        !TryGetMethodReference(compiledAssemblyDef.MainModule, typeDefinition, ref rpc, out targetMethodRef))
                    {
                        Debug.LogError($"Unable to find method signature: \"{rpc.methodName}\".");
                        goto failure;
                    }

                    var executionStage = (RPCExecutionStage)serializedRPC.rpcExecutionStage;
                    var targetMethodDef = targetMethodRef.Resolve();
                    if (!(targetMethodDef.IsStatic ? onTryStaticCallProcessor : onTryCallProcessor).ProcessMethodDef(
                        serializedRPC.rpcId,
                        targetMethodRef,
                        executionStage))
                        goto failure;

                    // Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

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

            var rpcMethodCustomAttributeType = typeof(RPC);

            var rpcMethodCustomAttributeTypeRef = compiledAssemblyDef.MainModule.ImportReference(rpcMethodCustomAttributeType);
            string rpcMethodAttributeFullName = rpcMethodCustomAttributeType.FullName;

            var rpcMethodAttributeRPCExecutionStageArgument = compiledAssemblyDef.MainModule.ImportReference(typeof(RPCExecutionStage));
            var methodDefs = compiledAssemblyDef.Modules
                .SelectMany(moduleDef => moduleDef.Types
                    .SelectMany(type => type.Methods
                        .Where(method => method.CustomAttributes
                            .Any(customAttribute => 
                            customAttribute.HasConstructorArguments &&
                            customAttribute.AttributeType.FullName == rpcMethodAttributeFullName))));

            foreach (var targetRPCMethodDef in methodDefs)
            {
                // Debug.Log($"Post Processing method: \"{targetRPCMethodDef.Name}\" in type: \"{targetRPCMethodDef.DeclaringType.FullName}\".");

                var customAttribute = targetRPCMethodDef.CustomAttributes.First(ca => ca.AttributeType.FullName == rpcMethodAttributeFullName);
                if (!TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPC.RPCExecutionStageMarker>(customAttribute, out var rpcExecutionStageAttributeArgumentIndex) ||
                    !TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<RPC.RPCIDMarker>(customAttribute, out var rpcIdAttributeArgumentIndex))
                    goto failure;

                var rpcExecutionStageAttributeArgument = customAttribute.ConstructorArguments[rpcExecutionStageAttributeArgumentIndex];

                ushort newRPCId = unusedRPCIds.Count > 0 ? unusedRPCIds.Dequeue() : (ushort)++lastRPCId;

                RPCExecutionStage executionStage = (RPCExecutionStage)rpcExecutionStageAttributeArgument.Value;
                if (!(targetRPCMethodDef.IsStatic ? onTryStaticCallProcessor : onTryCallProcessor).ProcessMethodDef(
                    newRPCId,
                    targetRPCMethodDef,
                    executionStage))
                    goto failure;

                var customAttributeArgument = customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex];
                customAttribute.ConstructorArguments[rpcIdAttributeArgumentIndex] = new CustomAttributeArgument(customAttributeArgument.Type, newRPCId);
            }

            if (!onTryCallProcessor.TryInjectSwitchCaseForImmediateRPCExecutionMethod() ||
                !onTryStaticCallProcessor.TryInjectSwitchCaseForImmediateRPCExecutionMethod() ||
                !TryInjectDefaultSwitchCaseForExecutionStageMethods())
                goto failure;

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), 
                SymbolStream = pdb, 
                WriteSymbols = true,
            };

            try
            {
                compiledAssemblyDef.Write(pe, writerParameters);
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                goto failure;
            }

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));

            ignoreAssembly:
            return null;

            failure:
            Debug.LogError($"Failure occurred while attempting to post process assembly: \"{compiledAssembly.Name}\".");
            return null;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
