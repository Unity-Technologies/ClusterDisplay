﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Collections;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public partial class RPCILPostProcessor
    {
        private static bool TryInjectAppendStaticSizedRPCCall (
            ILProcessor il, 
            Instruction afterInstruction, 
            bool isStatic, 
            int rpcId, 
            int rpcExecutionStage,
            MethodReference call, 
            int sizeOfAllParameters,
            out Instruction lastInstruction)
        {
            if (call == null)
            {
                Debug.LogError($"Unable to inject call into method body: \"{il.Body.Method.Name}\" declared in: \"{il.Body.Method.DeclaringType.Name}\", the call is null!");
                lastInstruction = null;
                return false;
            }

            if (afterInstruction == null)
            {
                Debug.LogError($"Unable to inject call to: \"{call.Name}\" declared in: \"{call.DeclaringType.FullName}\", the instruction to inject after is null!");
                lastInstruction = null;
                return false;
            }

            if (isStatic)
                CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcId);

            else
            {
                CecilUtils.InsertPushThisAfter(il, ref afterInstruction);
                CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcId);
            }

            CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            // InsertPushIntAfter(il, ref afterInstruction, ((RPCExecutionStage)rpcExecutionStage) != RPCExecutionStage.Automatic ? 1 : 0);
            CecilUtils.InsertPushIntAfter(il, ref afterInstruction, sizeOfAllParameters);
            CecilUtils.InsertCallAfter(il, ref afterInstruction, call);
            lastInstruction = afterInstruction;

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
            var debugTypeRef = CecilUtils.Import(moduleDef, debugType);
            var debugTypeDef = debugTypeRef.Resolve();
            var logMethodRef = CecilUtils.Import(moduleDef, debugTypeDef.Methods.Where(method => {
                return
                    method.Name == "Log" &&
                    method.Parameters.Count == 1;
            }).FirstOrDefault());

            lastInstruction = null;

            CecilUtils.InsertPushStringAfter(il, ref afterInstruction, message);
            CecilUtils.InsertCallAfter(il, ref afterInstruction, logMethodRef);
        }

        private bool TryFindMethodWithMatchingFormalySerializedAs (
            ModuleDefinition moduleDef, 
            TypeDefinition typeDefinition, 
            string serializedMethodName, 
            out MethodReference outMethodRef)
        {
            if (typeDefinition == null)
            {
                Debug.LogError($"Unable to find serialized method: \"{serializedMethodName}\", the type definition is null!");
                outMethodRef = null;
                return false;
            }

            var rpcMethodAttributeType = typeof(ClusterRPC);
            var stringType = typeof(string);

            foreach (var methodDef in typeDefinition.Methods)
            {
                if (!methodDef.HasCustomAttributes)
                    continue;

                foreach (var customAttribute in methodDef.CustomAttributes)
                {
                    if (!customAttribute.HasConstructorArguments ||
                        customAttribute.AttributeType.Namespace != rpcMethodAttributeType.Namespace ||
                        customAttribute.AttributeType.Name != rpcMethodAttributeType.Name)
                        continue;

                    foreach (var constructorArgument in customAttribute.ConstructorArguments)
                    {
                        if (constructorArgument.Type.Namespace != stringType.Namespace || 
                            constructorArgument.Type.Name != stringType.Name)
                            continue;

                        string formarlySerializedAs = constructorArgument.Value as string;
                        if (string.IsNullOrEmpty(formarlySerializedAs))
                            continue;

                        if (serializedMethodName != formarlySerializedAs)
                            continue;

                        outMethodRef = CecilUtils.Import(moduleDef, methodDef);
                        Debug.LogFormat($"Found renamed method: \"{outMethodRef.Name}\" that was previously named as: \"{serializedMethodName}\".");
                        return true;
                    }
                }
            }

            // Debug.LogError($"Unable to find method signature: \"{serializedMethodName}\" declared in type: \"{typeDefinition.Name}\".");
            outMethodRef = null;
            return false;
        }

        public static MethodReference CreateMethodReferenceForGenericInstanceType (
            MethodReference methodReference,
            TypeReference declaringType) =>
            new MethodReference(
                methodReference.Name,
                methodReference.ReturnType,
                declaringType)
            {
                CallingConvention = methodReference.CallingConvention,
                HasThis = methodReference.HasThis,
                ExplicitThis = methodReference.ExplicitThis,
                MetadataToken = methodReference.MetadataToken,
            };

        private static bool TryGetLengthPropertyGetMethod (ModuleDefinition moduleDef, ParameterDefinition param, out MethodReference lengthPropertyGetMethodReference)
        {
            lengthPropertyGetMethodReference = null;
            var parameterTypeRef = CecilUtils.Import(moduleDef, param.ParameterType);
            var lengthPropertyDef = param.ParameterType.Resolve().Properties.FirstOrDefault(propertyDef =>
            {
                return
                    propertyDef.GetMethod != null &&
                    propertyDef.Name == "Length";
            });

            if (lengthPropertyDef == null || lengthPropertyDef.GetMethod == null)
                return false;

            lengthPropertyGetMethodReference = CreateMethodReferenceForGenericInstanceType(CecilUtils.Import(moduleDef, lengthPropertyDef.GetMethod), param.ParameterType);
            return true;
        }

        private static bool TryGetArrayLengthGetProperty (ModuleDefinition moduleDef, ParameterDefinition param, out MethodReference lengthPropertyGetMethodReference)
        {
            lengthPropertyGetMethodReference = null;
            var parameterTypeRef = CecilUtils.Import(param.ParameterType.Module, param.ParameterType);
            var lengthPropertyDef = parameterTypeRef.Resolve().Properties.FirstOrDefault(propertyDef =>
            {
                return
                    propertyDef.GetMethod != null &&
                    propertyDef.Name == "Length";
            });

            if (lengthPropertyDef == null || lengthPropertyDef.GetMethod == null)
                return false;

            lengthPropertyGetMethodReference = CreateMethodReferenceForGenericInstanceType(CecilUtils.Import(moduleDef, lengthPropertyDef.GetMethod), param.ParameterType);
            return true;
        }

        private static bool TryInjectBridgeToDynamicallySizedRPCPropagation (
            Type rpcEmitterType,
            int rpcId,
            int rpcExecutionStage,
            MethodReference targetMethodRef,
            ILProcessor il,
            ref Instruction afterInstruction,
            MethodReference appendRPCMethodRef,
            int totalSizeOfStaticallySizedRPCParameters)
        {
            var targetMethodDef = targetMethodRef.Resolve();

            if (targetMethodDef.IsStatic)
                CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcId);

            else
            {
                CecilUtils.InsertPushThisAfter(il, ref afterInstruction);
                CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcId);
            }

            CecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            // InsertPushIntAfter(il, ref afterInstruction, ((RPCExecutionStage)rpcExecutionStage) != RPCExecutionStage.Automatic ? 1 : 0);
            CecilUtils.InsertPushIntAfter(il, ref afterInstruction, totalSizeOfStaticallySizedRPCParameters);

            if (targetMethodRef.HasParameters)
            {
                // Loop through each array/string parameter adding the runtime byte size to total parameters payload size.
                foreach (var param in targetMethodRef.Parameters)
                {
                    if (CecilUtils.ParameterIsString(targetMethodRef.Module, param))
                    {
                        CecilUtils.InsertPushIntAfter(il, ref afterInstruction, 1);
                        CecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: targetMethodDef.IsStatic, byReference: param.IsOut || param.IsIn);

                        var stringTypeDef = targetMethodDef.Module.TypeSystem.String.Resolve();
                        var stringLengthPropertyRef = stringTypeDef.Properties.FirstOrDefault(propertyDef =>
                        {
                            return
                                propertyDef.GetMethod != null &&
                                propertyDef.Name == "Length";
                        });

                        if (stringLengthPropertyRef == null)
                        {
                            Debug.LogError($"Unable to find Length property for parameter type: \"{param.ParameterType.FullName}\".");
                            return false;
                        }

                        var stringLengthGetterMethodDef = stringLengthPropertyRef.GetMethod;
                        if (stringLengthGetterMethodDef == null)
                        {
                            Debug.LogError($"Unable to find Length property getter method for parameter type: \"{param.ParameterType.FullName}\".");
                            return false;
                        }

                        CecilUtils.InsertCallAfter(il, ref afterInstruction, CecilUtils.Import(targetMethodRef.Module, stringLengthGetterMethodDef)); // Call string length getter with pushes the string length to the stack.
                        CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply char size of one byte by the length of the string.
                        CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add string size in bytes to total parameters payload size.
                        CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_2); // Load "2" as a constant which we designate as the array's byte size.
                        CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                    }

                    else if (param.ParameterType.IsGenericInstance)
                    {
                        bool isNativeCollection = ParameterIsNativeCollection(param);
                        bool isArray = param.ParameterType.IsArray || isNativeCollection;

                        if (isArray)
                        {
                            var elementType = CecilUtils.Import(targetMethodRef.Module, isNativeCollection ? (param.ParameterType as GenericInstanceType).GenericArguments[0] : param.ParameterType.GetElementType());
                            if (!TryGetLengthPropertyGetMethod(targetMethodRef.Module, param, out var lengthPropertyGetMethodRef))
                                return false;

                            int arrayElementSize = 0;
                            if (!CecilUtils.TryDetermineSizeOfValueType(elementType.Resolve(), ref arrayElementSize))
                                return false;

                            CecilUtils.InsertPushIntAfter(il, ref afterInstruction, arrayElementSize); // Push array element size to stack.
                            if (isNativeCollection)
                            {
                                // If the parameter is a native collection, we need to use a reference since it's a struct.
                                var newInstruction = Instruction.Create(OpCodes.Ldarga_S, param);
                                il.InsertAfter(afterInstruction, newInstruction);
                                afterInstruction = newInstruction;
                            }

                            else CecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: targetMethodDef.IsStatic, byReference: param.IsOut || param.IsIn);

                            if (lengthPropertyGetMethodRef == null)
                            {
                                Debug.LogError($"Unable to find Length property for parameter type: \"{param.ParameterType.FullName}\".");
                                return false;
                            }

                            CecilUtils.InsertCallAfter(il, ref afterInstruction, CecilUtils.Import(targetMethodRef.Module, lengthPropertyGetMethodRef)); // Call array length getter which will push array length to stack.
                            CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply array element size by array length.
                            CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add total array size in bytes to total parameters payload size.
                            CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_2); // Load "2" as a constant which we designate as the array's byte size.
                            CecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                        }
                    }
                }
            }

            CecilUtils.InsertCallAfter(il, ref afterInstruction, appendRPCMethodRef);

            if (targetMethodRef.HasParameters)
            {
                // Loop through the parameters again to inject instructions to push each parameter values to RPC buffer.
                foreach (var param in targetMethodRef.Parameters)
                {
                    GenericInstanceMethod genericInstanceMethod = null;
                    var paramDef = param.Resolve();

                    if (CecilUtils.ParameterIsString(targetMethodRef.Module, param))
                    {
                        if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCStringParameterValueMarker>(rpcEmitterType, out var appendRPCStringParameterValueMethodInfo))
                            return false;
                        var appendRPCStringParameterValueMethodRef = CecilUtils.Import(targetMethodRef.Module, appendRPCStringParameterValueMethodInfo);

                        CecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                        CecilUtils.InsertCallAfter(il, ref afterInstruction, appendRPCStringParameterValueMethodRef);
                    }
                    else
                    {
                        if (ParameterIsNativeCollection(param))
                        {
                            MethodInfo appendMethodInfo = null;
                            if (param.ParameterType.Name == typeof(NativeArray<>).Name)
                            {
                                if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCNativeArrayParameterValueMarker>(rpcEmitterType, out appendMethodInfo))
                                    return false;
                            }

                            else if (param.ParameterType.Name == typeof(NativeSlice<>).Name)
                            {
                                if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCNativeSliceParameterValueMarker>(rpcEmitterType, out appendMethodInfo))
                                    return false;
                            }

                            else
                            {
                                Debug.LogError($"Unhandled native collection of type: \"{param.ParameterType.FullName}\".");
                                return false;
                            }

                            var appendMethodRef = CecilUtils.Import(targetMethodRef.Module, appendMethodInfo);
                            genericInstanceMethod = new GenericInstanceMethod(appendMethodRef);
                            var elementType = (param.ParameterType as GenericInstanceType).GenericArguments[0]; // Since NativeArray<> has a generic argument, we want a generic instance method with that argument.
                            genericInstanceMethod.GenericArguments.Add(elementType);
                        }

                        else if (param.ParameterType.IsArray)
                        {
                            if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCArrayParameterValueMarker>(rpcEmitterType, out var appendRPCArrayParameterValueMethodInfo))
                                return false;

                            var appendRPCArrayParameterValueMethodRef = CecilUtils.Import(targetMethodRef.Module, appendRPCArrayParameterValueMethodInfo);
                            genericInstanceMethod = new GenericInstanceMethod(appendRPCArrayParameterValueMethodRef);

                            var elementType = param.ParameterType.GetElementType(); // Get the array's element type which is the int of the int[].
                            genericInstanceMethod.GenericArguments.Add(elementType);
                        }

                        else
                        {
                            if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendRPCValueTypeParameterValueMethodInfo))
                                return false;

                            var appendRPCValueTypeParameterValueMethodRef = CecilUtils.Import(targetMethodRef.Module, appendRPCValueTypeParameterValueMethodInfo);
                            genericInstanceMethod = new GenericInstanceMethod(appendRPCValueTypeParameterValueMethodRef);
                            genericInstanceMethod.GenericArguments.Add(param.ParameterType);
                        }

                        CecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                        CecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                    }
                }
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));
            return true;
        }

        private static bool TryInjectBridgeToStaticallySizedRPCPropagation (
            Type rpcEmitterType,
            int rpcId,
            int rpcExecutionStage,
            MethodReference targetMethodRef,
            ILProcessor il,
            ref Instruction afterInstruction,
            MethodReference appendRPCMethodRef,
            int totalSizeOfStaticallySizedRPCParameters)
        {
            if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendRPCValueTypeParameterValueMethodInfo))
                return false;

            var targetMethodDef = targetMethodRef.Resolve();

            var appendRPCValueTypeParameterValueMethodRef = CecilUtils.Import(targetMethodRef.Module, appendRPCValueTypeParameterValueMethodInfo);

            if (!TryInjectAppendStaticSizedRPCCall(
                il,
                afterInstruction,
                targetMethodDef.IsStatic,
                rpcId,
                rpcExecutionStage,
                appendRPCMethodRef,
                totalSizeOfStaticallySizedRPCParameters,
                out afterInstruction))
                return false;

            if (targetMethodRef.HasParameters)
            {
                foreach (var paramDef in targetMethodRef.Parameters)
                {
                    var genericInstanceMethod = new GenericInstanceMethod(appendRPCValueTypeParameterValueMethodRef);
                    genericInstanceMethod.GenericArguments.Add(paramDef.ParameterType);

                    CecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, paramDef.IsOut || paramDef.IsIn);
                    CecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                }
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));

            return true;
        }

        private static bool ParameterIsNativeCollection (
            ParameterDefinition parameterDefinition) => 
                parameterDefinition.ParameterType.IsValueType &&
                parameterDefinition.ParameterType.Namespace == typeof(NativeArray<>).Namespace;

        private static bool TryPollParameterInformation (
            ModuleDefinition moduleDef, 
            MethodReference targetMethodRef, 
            out int totalSizeOfStaticallySizedRPCParameters, 
            out bool hasDynamicallySizedRPCParameters)
        {
            totalSizeOfStaticallySizedRPCParameters = 0;
            hasDynamicallySizedRPCParameters = false;

            foreach (var param in targetMethodRef.Parameters)
            {
                var typeReference = CecilUtils.Import(moduleDef, param.ParameterType);
                if (typeReference.IsArray || CecilUtils.ParameterIsString(moduleDef, param) || ParameterIsNativeCollection(param))
                {
                    hasDynamicallySizedRPCParameters = true;
                    continue;
                }

                else if (typeReference.IsValueType)
                {
                    int sizeOfType = 0;
                    if (!CecilUtils.TryDetermineSizeOfValueType(typeReference.Resolve(), ref sizeOfType))
                        return false;

                    if (sizeOfType > ushort.MaxValue)
                    {
                        Debug.LogError($"Unable to post process method: \"{targetMethodRef.Name}\" declared in: \"{targetMethodRef.DeclaringType.FullName}\", the parameter: \"{param.Name}\" of type: \"{typeReference.FullName}\" is larger then the max parameter size of: {ushort.MaxValue} bytes.");
                        return false;
                    }

                    int totalBytesNow = ((int)totalSizeOfStaticallySizedRPCParameters) + sizeOfType;
                    if (totalBytesNow > ushort.MaxValue)
                    {
                        Debug.LogError($"Unable to post process method: \"{targetMethodRef.Name}\" declared in: \"{targetMethodRef.DeclaringType.FullName}\", the parameter: \"{param.Name}\" pushes the total parameter payload size to: {totalBytesNow} bytes, the max parameters payload size is: {ushort.MaxValue} bytes.");
                        return false;
                    }

                    totalSizeOfStaticallySizedRPCParameters += (ushort)sizeOfType;
                }
            }

            return true;
        }

        private static bool TryImportTryGetSingletonObject<T> (
            ModuleDefinition moduleDefinition,
            out TypeReference typeRef,
            out MethodReference tryGetInstanceMethodRef)
        {
            var type = typeof(T);
            typeRef = CecilUtils.Import(moduleDefinition, type);

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

            var baseTypeRef = CecilUtils.Import(moduleDefinition, type.BaseType);
            if (baseTypeRef == null)
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericType = new GenericInstanceType(baseTypeRef);
            baseGenericType.GenericArguments.Add(typeRef);

            if (!CecilUtils.TryFindMethodWithAttribute<SingletonScriptableObjectTryGetInstanceMarker>(type.BaseType, out var tryGetInstanceMethod))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericMethodRef = CecilUtils.Import(moduleDefinition, tryGetInstanceMethod);

            return (tryGetInstanceMethodRef = baseGenericMethodRef) != null;
        }

        private static bool TryGetDerrivedType (
            AssemblyDefinition assemblyDef,
            TypeDefinition targetTypeDef,
            out TypeReference derrivedTypeRef)
        {
            var derrivedTypeDef = assemblyDef.MainModule.GetTypes()
                .Where(typeDef => 
                    typeDef != null && 
                    typeDef.BaseType != null && 
                    typeDef.BaseType.FullName == targetTypeDef.FullName).FirstOrDefault();

            if (derrivedTypeDef == null)
            {
                derrivedTypeRef = null;
                return false;
            }

            derrivedTypeRef = CecilUtils.Import(assemblyDef.MainModule, derrivedTypeDef);
            return true;
        }

        private static bool TryFindMethodReferenceWithAttributeInModule (
            ModuleDefinition moduleDef,
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

            methodRef = CecilUtils.Import(moduleDef, methodDef);
            return true;
        }

        private static bool TryCreateRPCILClassConstructor (
            AssemblyDefinition compiledAssemblyDef, 
            MethodReference onTryCallInstanceMethodDef,
            MethodReference onTryStaticCallInstanceMethodDef,
            MethodReference executeQueuedRPCMethodRef,
            out MethodDefinition constructorMethodDef)
        {
            constructorMethodDef = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public, compiledAssemblyDef.MainModule.TypeSystem.Void);
            var constructorILProcessor = constructorMethodDef.Body.GetILProcessor();

            constructorILProcessor.Emit(OpCodes.Ldarg_0);

            if (!CecilUtils.TryPushMethodRef<RPCInterfaceRegistry.OnTryCallDelegateMarker>(compiledAssemblyDef, onTryCallInstanceMethodDef, constructorILProcessor) ||
                !CecilUtils.TryPushMethodRef<RPCInterfaceRegistry.OnTryStaticCallDelegateMarker>(compiledAssemblyDef, onTryStaticCallInstanceMethodDef, constructorILProcessor) || 
                !CecilUtils.TryPushMethodRef<RPCInterfaceRegistry.ExecuteQueuedRPCDelegateMarker>(compiledAssemblyDef, executeQueuedRPCMethodRef, constructorILProcessor))
            {
                constructorMethodDef = null;
                return false;
            }

            var constructorMethodInfo = typeof(RPCInterfaceRegistry).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(constructorInfo => constructorInfo.GetCustomAttribute<RPCInterfaceRegistry.RPCInterfaceRegistryConstuctorMarker>() != null);
            constructorILProcessor.Emit(OpCodes.Call, CecilUtils.Import(compiledAssemblyDef.MainModule, constructorMethodInfo));
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Ret);

            return true;
        }

        private static bool InjectPushOfRPCParamters (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            MethodReference targetMethodRef,
            ParameterDefinition bufferPosParamDef,
            bool isImmediateRPCExeuction,
            ref Instruction afterInstruction)
        {
            foreach (var paramDef in targetMethodRef.Parameters)
            {
                if (CecilUtils.ParameterIsString(targetMethodRef.Module, paramDef))
                {
                    if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseStringMarker>(typeof(RPCBufferIO), out var parseStringMethod))
                        return false;

                    var parseStringMethodRef = CecilUtils.Import(moduleDef, parseStringMethod);

                    CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                    CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, parseStringMethodRef);
                }

                else if (ParameterIsNativeCollection(paramDef))
                {
                    var elementType = (paramDef.ParameterType as GenericInstanceType).GenericArguments[0];
                    if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseNativeCollectionMarker>(typeof(RPCBufferIO), out var parseNativeArrayMethodInfo))
                        return false;

                    var parseNativeArrayMethodRef = CecilUtils.Import(moduleDef, parseNativeArrayMethodInfo);

                    var genericInstanceMethod = new GenericInstanceMethod(parseNativeArrayMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = CecilUtils.Import(moduleDef, elementType);
                    genericInstanceMethod.GenericArguments.Add(paramRef);

                    CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                    CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, CecilUtils.Import(moduleDef, genericInstanceMethod));
                }

                else if (paramDef.ParameterType.IsArray)
                {
                    var elementType = paramDef.ParameterType.GetElementType();
                    if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseArrayMarker>(typeof(RPCBufferIO), out var parseArrayMethod))
                        return false;

                    var parseArrayMethodRef = CecilUtils.Import(moduleDef, parseArrayMethod);

                    var genericInstanceMethod = new GenericInstanceMethod(parseArrayMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = CecilUtils.Import(moduleDef, elementType);
                    genericInstanceMethod.GenericArguments.Add(paramRef);

                    CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                    CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, CecilUtils.Import(moduleDef, genericInstanceMethod));
                }

                else if (paramDef.ParameterType.IsValueType)
                {
                    if (!CecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseStructureMarker>(typeof(RPCBufferIO), out var parseStructureMethod))
                        return false;
                    var parseStructureMethodRef = CecilUtils.Import(moduleDef, parseStructureMethod);

                    var genericInstanceMethod = new GenericInstanceMethod(parseStructureMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = CecilUtils.Import(moduleDef, paramDef.ParameterType);
                    // paramRef.IsValueType = true;
                    genericInstanceMethod.GenericArguments.Add(paramRef);
                    var genericInstanceMethodRef = CecilUtils.Import(moduleDef, genericInstanceMethod);

                    CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !isImmediateRPCExeuction);
                    CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, genericInstanceMethodRef);  // Call generic method to convert bytes into our struct.
                }
            }

            return true;
        }

        private static bool TryInjectInstanceRPCExecution (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            Instruction beforeInstruction,
            MethodReference targetMethod,
            bool isImmediateRPCExeuction,
            out Instruction firstInstructionOfInjection)
        {
            if (beforeInstruction == null)
            {
                Debug.LogError("Unable to inject instance RPC execution IL isntructions, the point at which we want to inject instructions before is null!");
                firstInstructionOfInjection = null;
                return false;
            }

            var method = ilProcessor.Body.Method;
            if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(method, out var pipeIdParamDef) ||
                !CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            if (!TryGetGetInstanceMethodRef(moduleDef, out var getInstanceMEthodRef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            var afterInstruction = firstInstructionOfInjection = CecilUtils.InsertPushParameterToStackBefore(ilProcessor, beforeInstruction, pipeIdParamDef, isStaticCaller: method.IsStatic, byReference: false); // Load pipeId parameter onto stack.
            CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, getInstanceMEthodRef);

            if (!targetMethod.HasParameters)
            {
                // Call method on target object without any parameters.
                CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, targetMethod);

                if (isImmediateRPCExeuction)
                    CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            InjectPushOfRPCParamters(
                moduleDef,
                ilProcessor,
                targetMethod,
                 bufferPosParamDef,
                 isImmediateRPCExeuction,
                 ref afterInstruction);

            CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, targetMethod);

            if (isImmediateRPCExeuction)
                CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

            CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
            return true;
        }

        private static bool TryInjectStaticRPCExecution (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            Instruction beforeInstruction,
            MethodReference targetMethodRef,
            bool isImmediateRPCExeuction,
            out Instruction firstInstructionOfInjection)
        {
            var method = ilProcessor.Body.Method;
            if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            Instruction afterInstruction = null;

            var voidTypeRef = CecilUtils.Import(ilProcessor.Body.Method.Module, typeof(void));
            if (!targetMethodRef.HasParameters)
            {
                // Call method on target object without any parameters.
                afterInstruction = firstInstructionOfInjection = CecilUtils.InsertCallBefore(ilProcessor, beforeInstruction, targetMethodRef);

                if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                    CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

                if (isImmediateRPCExeuction)
                    CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            afterInstruction = firstInstructionOfInjection = CecilUtils.InsertBefore(ilProcessor, beforeInstruction, OpCodes.Nop);

            InjectPushOfRPCParamters(
                moduleDef,
                ilProcessor,
                targetMethodRef,
                 bufferPosParamDef,
                 isImmediateRPCExeuction,
                 ref afterInstruction);

            CecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, targetMethodRef);

            if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

            if (isImmediateRPCExeuction)
                CecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

            CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
            return true;
        }

        private static bool TryInjectSwitchCaseForRPC (
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

            if (!CecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCIdMarker>(ilProcessor.Body.Method, out var rpcIdParamDef))
            {
                lastInstructionOfSwitchJmp = null;
                return false;
            }

            CecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcIdParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: false);

            CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ldc_I4, valueToPushForBeq);
            CecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Beq, jmpToInstruction);
            lastInstructionOfSwitchJmp = afterInstruction;

            return true;
        }

        private static bool TryGenerateRPCILTypeInCompiledAssembly (AssemblyDefinition compiledAssemblyDef, out TypeReference rpcInterfaceRegistryDerrivedTypeRef)
        {
            var newTypeDef = new TypeDefinition("Unity.ClusterDisplay.Generated", "RPCIL", Mono.Cecil.TypeAttributes.NestedPrivate);
            newTypeDef.BaseType = CecilUtils.Import(compiledAssemblyDef.MainModule, typeof(RPCInterfaceRegistry));

            var rpcIdParameterDef = new ParameterDefinition("rpcId", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            CecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCIdMarker>(compiledAssemblyDef, rpcIdParameterDef);

            var pipeParameterDef = new ParameterDefinition("pipeId", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            CecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.PipeIdMarker>(compiledAssemblyDef, pipeParameterDef);

            var parametersPayloadSize = new ParameterDefinition("parametersPayloadSize", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            CecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(compiledAssemblyDef, parametersPayloadSize);

            var rpcBufferParameterPositionRef = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.In, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            var rpcBufferParameterPosition = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);

            CecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, rpcBufferParameterPositionRef);
            CecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, rpcBufferParameterPosition);

            var onTryCallInstanceMethodDef = new MethodDefinition("OnTryCallInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryCallInstanceMethodDef.Parameters.Add(rpcIdParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(pipeParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPositionRef);
            CecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.OnTryCallMarker>(compiledAssemblyDef.MainModule, onTryCallInstanceMethodDef);
            var il = onTryCallInstanceMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            var onTryStaticCallInstanceMethodDef = new MethodDefinition("OnTryStaticCallInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcIdParameterDef);
            onTryStaticCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPositionRef);
            CecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.OnTryStaticCallMarker>(compiledAssemblyDef.MainModule, onTryStaticCallInstanceMethodDef);
            il = onTryStaticCallInstanceMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            var executeQueuedRPCMethodDef = new MethodDefinition("ExecuteQueuedRPC", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Void);
            executeQueuedRPCMethodDef.Parameters.Add(rpcIdParameterDef);
            executeQueuedRPCMethodDef.Parameters.Add(pipeParameterDef);
            executeQueuedRPCMethodDef.Parameters.Add(parametersPayloadSize);
            executeQueuedRPCMethodDef.Parameters.Add(rpcBufferParameterPosition);
            CecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.ExecuteQueuedRPC>(compiledAssemblyDef.MainModule, executeQueuedRPCMethodDef);
            il = executeQueuedRPCMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ret);

            newTypeDef.Methods.Add(onTryCallInstanceMethodDef);
            newTypeDef.Methods.Add(onTryStaticCallInstanceMethodDef);
            newTypeDef.Methods.Add(executeQueuedRPCMethodDef);

            if (!TryCreateRPCILClassConstructor(
                compiledAssemblyDef, 
                onTryCallInstanceMethodDef,
                onTryStaticCallInstanceMethodDef,
                executeQueuedRPCMethodDef,
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
    }
}
