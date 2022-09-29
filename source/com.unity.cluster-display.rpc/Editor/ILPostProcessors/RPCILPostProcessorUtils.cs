using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Collections;
using UnityEngine;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    internal partial class RPCILPostProcessor
    {
        bool TryInjectAppendStaticSizedRPCCall (
            ILProcessor il, 
            Instruction afterInstruction, 
            bool isStatic, 
            string rpcHash, 
            ushort rpcExecutionStage,
            MethodReference call, 
            buint sizeOfAllParameters,
            out Instruction lastInstruction)
        {
            if (call == null)
            {
                logger.LogError($"Unable to inject call into method body: \"{il.Body.Method.Name}\" declared in: \"{il.Body.Method.DeclaringType.Name}\", the call is null!");
                lastInstruction = null;
                return false;
            }

            if (afterInstruction == null)
            {
                logger.LogError($"Unable to inject call to: \"{call.Name}\" declared in: \"{call.DeclaringType.FullName}\", the instruction to inject after is null!");
                lastInstruction = null;
                return false;
            }

            if (isStatic)
                cecilUtils.InsertPushStringAfter(il, ref afterInstruction, rpcHash);

            else
            {
                cecilUtils.InsertPushThisAfter(il, ref afterInstruction);
                cecilUtils.InsertPushStringAfter(il, ref afterInstruction, rpcHash);
            }

            cecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            cecilUtils.InsertPushBufferUIntAfter(il, ref afterInstruction, sizeOfAllParameters);
            cecilUtils.InsertCallAfter(il, ref afterInstruction, call);
            lastInstruction = afterInstruction;

            return true;
        }

        bool TryFindMethodWithMatchingFormalySerializedAs (
            ModuleDefinition moduleDef, 
            TypeDefinition typeDefinition, 
            RPCStub rpcStub,
            string serializedMethodName, 
            out MethodReference outMethodRef)
        {
            rpcStub.methodStub.methodName = serializedMethodName;
            return cecilUtils.TryGetMethodReference(moduleDef, typeDefinition, ref rpcStub, out outMethodRef);
        }

        public MethodReference CreateMethodReferenceForGenericInstanceType (
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
        
        bool TryGetArrayLengtyhPropertyGetMethod (ModuleDefinition moduleDef, TypeReference elementType, out MethodReference lengthPropertyGetMethodReference)
        {
            var getLengthMethodInfo = typeof(System.Array).GetMethod("get_Length");
            cecilUtils.TryImport(moduleDef, getLengthMethodInfo, out lengthPropertyGetMethodReference);
            return true;
        }

        bool TryGetNativeArrayLengtyhPropertyGetMethod (ModuleDefinition moduleDef, ParameterDefinition param, out MethodReference lengthPropertyGetMethodReference)
        {
            lengthPropertyGetMethodReference = null;
            var parameterTypeRef = cecilUtils.Import(moduleDef, param.ParameterType);
            var lengthPropertyDef = param.ParameterType.Resolve().Properties.FirstOrDefault(propertyDef =>
            {
                return
                    propertyDef.GetMethod != null &&
                    propertyDef.Name == "Length";
            });

            if (lengthPropertyDef == null || lengthPropertyDef.GetMethod == null)
                return false;

            lengthPropertyGetMethodReference = CreateMethodReferenceForGenericInstanceType(cecilUtils.Import(moduleDef, lengthPropertyDef.GetMethod), param.ParameterType);
            return true;
        }

        bool TryInjectBridgeToDynamicallySizedRPCPropagation (
            Type rpcEmitterType,
            string rpcHash,
            int rpcExecutionStage,
            ILProcessor il,
            ref Instruction afterInstruction,
            MethodReference appendRPCMethodRef,
            buint totalSizeOfStaticallySizedRPCParameters)
        {
            if (il.Body.Method.IsStatic)
                cecilUtils.InsertPushStringAfter(il, ref afterInstruction, rpcHash);

            else
            {
                cecilUtils.InsertPushThisAfter(il, ref afterInstruction);
                cecilUtils.InsertPushStringAfter(il, ref afterInstruction, rpcHash);
            }

            cecilUtils.InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            // InsertPushIntAfter(il, ref afterInstruction, ((RPCExecutionStage)rpcExecutionStage) != RPCExecutionStage.Automatic ? 1 : 0);
            cecilUtils.InsertPushBufferUIntAfter(il, ref afterInstruction, totalSizeOfStaticallySizedRPCParameters);

            if (il.Body.Method.HasParameters)
            {
                // Loop through each array/string parameter adding the runtime byte size to total parameters payload size.
                foreach (var param in il.Body.Method.Parameters)
                {
                    if (cecilUtils.ParameterIsString(il.Body.Method.Module, param))
                    {
                        cecilUtils.InsertPushIntAfter(il, ref afterInstruction, 2/* UTF-16 2 bytes */);
                        cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: il.Body.Method.IsStatic, byReference: param.IsOut || param.IsIn);

                        var getStringLengthMethodInfo = typeof(string).GetMethod("get_Length", BindingFlags.Public | BindingFlags.Instance);
                        cecilUtils.TryImport(il.Body.Method.Module, getStringLengthMethodInfo, out var getStringLengthMethodRef);
                        cecilUtils.InsertCallAfter(il, ref afterInstruction, cecilUtils.Import(il.Body.Method.Module, getStringLengthMethodRef)); // Call string length getter with pushes the string length to the stack.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply char size of one byte by the length of the string.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add string size in bytes to total parameters payload size.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_4); // Load "4" to add to the parameter payload total size as the array byte count.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                        continue;
                    }

                    bool isNativeArray = ParameterIsNativeArray(param);
                    bool isArray = param.ParameterType.IsArray || isNativeArray;

                    if (isArray)
                    {
                        var elementTypeRef = cecilUtils.Import(il.Body.Method.Module, isNativeArray ? (param.ParameterType as GenericInstanceType).GenericArguments[0] : param.ParameterType.GetElementType());
                        
                        var elementTypeDef = elementTypeRef.Resolve();
                        if (elementTypeDef == null)
                            throw new Exception($"Unable to resolve array element type: \"{elementTypeRef.Name}\".");
                        
                        MethodReference lengthPropertyGetMethodRef = null;
                        if (isNativeArray)
                        {
                            if (!TryGetNativeArrayLengtyhPropertyGetMethod(il.Body.Method.Module, param, out lengthPropertyGetMethodRef))
                                return false;
                        }
                        
                        else if (!TryGetArrayLengtyhPropertyGetMethod(il.Body.Method.Module, elementTypeRef,
                                out lengthPropertyGetMethodRef))
                                return false;

                        buint arrayElementSize = 0;
                        if (!cecilUtils.TryDetermineSizeOfValueType(elementTypeDef, ref arrayElementSize))
                            return false;

                        cecilUtils.InsertPushBufferUIntAfter(il, ref afterInstruction, arrayElementSize); // Push array element size to stack.
                        if (isNativeArray)
                        {
                            // If the parameter is a native collection, we need to use a reference since it's a struct.
                            var newInstruction = Instruction.Create(OpCodes.Ldarga_S, param);
                            il.InsertAfter(afterInstruction, newInstruction);
                            afterInstruction = newInstruction;
                        }

                        else cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: il.Body.Method.IsStatic, byReference: param.IsOut || param.IsIn);

                        if (lengthPropertyGetMethodRef == null)
                        {
                            logger.LogError($"Unable to find Length property for parameter type: \"{param.ParameterType.FullName}\".");
                            return false;
                        }

                        cecilUtils.InsertCallAfter(il, ref afterInstruction, cecilUtils.Import(il.Body.Method.Module, lengthPropertyGetMethodRef)); // Call array length getter which will push array length to stack.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply array element size by array length.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add total array size in bytes to total parameters payload size.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_4); // Load "4" to add to the parameter payload total size as the array byte count.
                        cecilUtils.InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                    }
                }
            }

            cecilUtils.InsertCallAfter(il, ref afterInstruction, appendRPCMethodRef);

            if (il.Body.Method.HasParameters)
            {
                // Loop through the parameters again to inject instructions to push each parameter values to RPC buffer.
                foreach (var param in il.Body.Method.Parameters)
                {
                    GenericInstanceMethod genericInstanceMethod = null;
                    var paramDef = param.Resolve();

                    if (cecilUtils.ParameterIsString(il.Body.Method.Module, param)) // Handle strings.
                    {
                        if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCStringParameterValueMarker>(rpcEmitterType, out var appendRPCStringParameterValueMethodInfo))
                            return false;

                        if (!cecilUtils.TryImport(il.Body.Method.Module, appendRPCStringParameterValueMethodInfo, out var appendRPCStringParameterValueMethodRef))
                            return false;

                        cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: il.Body.Method.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                        cecilUtils.InsertCallAfter(il, ref afterInstruction, appendRPCStringParameterValueMethodRef);
                    }
                    
                    else // Handle something that is NOT a string.
                    {
                        if (ParameterIsNativeArray(param)) // Native arrays.
                        {
                            MethodInfo appendMethodInfo = null;
                            if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCNativeArrayParameterValueMarker>(rpcEmitterType, out appendMethodInfo))
                                return false;

                            if (!cecilUtils.TryImport(il.Body.Method.Module, appendMethodInfo, out var appendMethodRef))
                                return false;

                            genericInstanceMethod = new GenericInstanceMethod(appendMethodRef);
                            var elementType = (param.ParameterType as GenericInstanceType).GenericArguments[0]; // Since NativeArray<> has a generic argument, we want a generic instance method with that argument.
                            genericInstanceMethod.GenericArguments.Add(elementType);
                            
                            cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: il.Body.Method.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                            cecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                        }

                        else if (param.ParameterType.IsArray) // Value type arrays.
                        {
                            if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCArrayParameterValueMarker>(rpcEmitterType, out var appendRPCArrayParameterValueMethodInfo))
                                return false;

                            if (!cecilUtils.TryImport(il.Body.Method.Module, appendRPCArrayParameterValueMethodInfo, out var appendRPCArrayParameterValueMethodRef))
                                return false;

                            genericInstanceMethod = new GenericInstanceMethod(appendRPCArrayParameterValueMethodRef);

                            var elementType = param.ParameterType.GetElementType(); // Get the array's element type which is the int of the int[].
                            genericInstanceMethod.GenericArguments.Add(elementType);
                            
                            cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: il.Body.Method.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                            cecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                        }

                        // For char types, we need to handle them as a special case to respect unicode. However, for chars embedded in
                        // structs, you will need to use [MarshalAsAttribute(UnmanagedType.U2)] to explicitly describe to Marshal that
                        // I want char to be 2 bytes instead of 1. Before changing this, you should inspect the matching byte to char in:
                        // "InjectPushOfRPCParamters"
                        else if (param.ParameterType.FullName == il.Body.Method.Module.TypeSystem.Char.FullName)
                        {
                            // There is a specific method for converting a char to bytes and appending it to the RPC buffer.
                            if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCCharParameterValueMarker>(rpcEmitterType, out var appendParameterMethodInfo))
                                return false;
                            
                            if (!cecilUtils.TryImport(il.Body.Method.Module, appendParameterMethodInfo, out var appendParameterMethodRef))
                                return false;

                            cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef,
                                isStaticCaller: il.Body.Method.IsStatic,
                                byReference: paramDef.IsOut || paramDef.IsIn);
                            cecilUtils.InsertCallAfter(il, ref afterInstruction, appendParameterMethodRef);
                        }

                        else  // Value types.
                        {
                            if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendParameterMethodInfo))
                                return false;
                            
                            if (!cecilUtils.TryImport(il.Body.Method.Module, appendParameterMethodInfo, out var appendParameterMethodRef))
                                return false;
                            
                            genericInstanceMethod = new GenericInstanceMethod(appendParameterMethodRef);
                            genericInstanceMethod.GenericArguments.Add(param.ParameterType);

                            cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: il.Body.Method.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                            cecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                        }
                    }
                }
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));
            return true;
        }

        bool TryInjectBridgeToStaticallySizedRPCPropagation (
            Type rpcEmitterType,
            string rpcHash,
            ushort rpcExecutionStage,
            ILProcessor il,
            ref Instruction afterInstruction,
            MethodReference appendRPCMethodRef,
            buint totalSizeOfStaticallySizedRPCParameters)
        {
            if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendRPCValueTypeParameterValueMethodInfo))
                return false;

            if (!cecilUtils.TryImport(il.Body.Method.Module, appendRPCValueTypeParameterValueMethodInfo, out var appendRPCValueTypeParameterValueMethodRef))
                return false;

            if (!TryInjectAppendStaticSizedRPCCall(
                il,
                afterInstruction,
                il.Body.Method.IsStatic,
                rpcHash,
                rpcExecutionStage,
                appendRPCMethodRef,
                totalSizeOfStaticallySizedRPCParameters,
                out afterInstruction))
                return false;

            if (il.Body.Method.HasParameters)
            {
                foreach (var paramDef in il.Body.Method.Parameters)
                {
                    var genericInstanceMethod = new GenericInstanceMethod(appendRPCValueTypeParameterValueMethodRef);
                    genericInstanceMethod.GenericArguments.Add(paramDef.ParameterType);

                    cecilUtils.InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: il.Body.Method.IsStatic, paramDef.IsOut || paramDef.IsIn);
                    cecilUtils.InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                }
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));

            return true;
        }

        bool ParameterIsNativeArray (
            ParameterDefinition parameterDefinition)
        {
            var nativeArrayType = typeof(NativeArray<>);
            return
                parameterDefinition.ParameterType.IsValueType &&
                parameterDefinition.ParameterType.Namespace == nativeArrayType.Namespace &&
                parameterDefinition.ParameterType.Name == nativeArrayType.Name;
        }

        bool TryPollParameterInformation (
            ModuleDefinition moduleDef, 
            MethodDefinition targetMethodDef, 
            out buint totalSizeOfStaticallySizedRPCParameters, 
            out bool hasDynamicallySizedRPCParameters)
        {
            totalSizeOfStaticallySizedRPCParameters = 0;
            hasDynamicallySizedRPCParameters = false;

            foreach (var param in targetMethodDef.Parameters)
            {
                var typeReference = cecilUtils.Import(moduleDef, param.ParameterType);
                if (typeReference.IsArray || cecilUtils.ParameterIsString(moduleDef, param) || ParameterIsNativeArray(param))
                {
                    hasDynamicallySizedRPCParameters = true;
                    continue;
                }

                else if (typeReference.IsValueType)
                {
                    buint sizeOfType = 0;
                    if (!cecilUtils.TryDetermineSizeOfValueType(typeReference.Resolve(), ref sizeOfType))
                        return false;

                    if (sizeOfType > buint.MaxValue)
                    {
                        logger.LogError($"Unable to post process method: \"{targetMethodDef.Name}\" declared in: \"{targetMethodDef.DeclaringType.FullName}\", the parameter: \"{param.Name}\" of type: \"{typeReference.FullName}\" is larger then the max parameter size of: {buint.MaxValue} bytes.");
                        return false;
                    }

                    buint totalBytesNow = totalSizeOfStaticallySizedRPCParameters + sizeOfType;
                    if (totalBytesNow > buint.MaxValue)
                    {
                        logger.LogError($"Unable to post process method: \"{targetMethodDef.Name}\" declared in: \"{targetMethodDef.DeclaringType.FullName}\", the parameter: \"{param.Name}\" pushes the total parameter payload size to: {totalBytesNow} bytes, the max parameters payload size is: {buint.MaxValue} bytes.");
                        return false;
                    }

                    totalSizeOfStaticallySizedRPCParameters += sizeOfType;
                }
            }

            return true;
        }

        bool TryImportTryGetSingletonObject<T> (
            ModuleDefinition moduleDefinition,
            out TypeReference typeRef,
            out MethodReference tryGetInstanceMethodRef)
        {
            var type = typeof(T);
            if (!cecilUtils.TryImport(moduleDefinition, type, out typeRef))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

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

            if (!cecilUtils.TryImport(moduleDefinition, type.BaseType, out var baseTypeRef))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            var baseGenericType = new GenericInstanceType(baseTypeRef);
            baseGenericType.GenericArguments.Add(typeRef);

            if (!cecilUtils.TryFindMethodWithAttribute<SingletonScriptableObjectTryGetInstanceMarker>(type.BaseType, out var tryGetInstanceMethod))
            {
                tryGetInstanceMethodRef = null;
                return false;
            }

            return cecilUtils.TryImport(moduleDefinition, tryGetInstanceMethod, out tryGetInstanceMethodRef);
        }

        bool TryGetDerrivedType (
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

            derrivedTypeRef = cecilUtils.Import(assemblyDef.MainModule, derrivedTypeDef);
            return true;
        }

        bool TryFindMethodReferenceWithAttributeInModule (
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
                logger.LogError($"Unable to find method definition with attribute: \"{attributeTypeRef.FullName}\" in type: \"{typeDef.FullName}\".");
                methodRef = null;
                return false;
            }

            methodRef = cecilUtils.Import(moduleDef, methodDef);
            return true;
        }

        bool TryCreateRPCILClassConstructor (
            AssemblyDefinition compiledAssemblyDef, 
            MethodReference onTryCallInstanceMethodDef,
            MethodReference onTryStaticCallInstanceMethodDef,
            MethodReference executeQueuedRPCMethodRef,
            out MethodDefinition constructorMethodDef)
        {
            constructorMethodDef = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public, compiledAssemblyDef.MainModule.TypeSystem.Void);
            var constructorILProcessor = constructorMethodDef.Body.GetILProcessor();

            constructorILProcessor.Emit(OpCodes.Ldarg_0);

            if (!cecilUtils.TryPushMethodRef<RPCInterfaceRegistry.OnTryCallInstanceDelegateAttribute>(compiledAssemblyDef, onTryCallInstanceMethodDef, constructorILProcessor) ||
                !cecilUtils.TryPushMethodRef<RPCInterfaceRegistry.OnTryCallStaticDelegateAttribute>(compiledAssemblyDef, onTryStaticCallInstanceMethodDef, constructorILProcessor) || 
                !cecilUtils.TryPushMethodRef<RPCInterfaceRegistry.OnTryCallQueuedInstanceDelegateAttribute>(compiledAssemblyDef, executeQueuedRPCMethodRef, constructorILProcessor))
            {
                constructorMethodDef = null;
                return false;
            }

            var constructorMethodInfo = typeof(RPCInterfaceRegistry).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(constructorInfo => constructorInfo.GetCustomAttribute<RPCInterfaceRegistry.RPCInterfaceRegistryConstuctorMarker>() != null);
            if (!cecilUtils.TryImport(compiledAssemblyDef.MainModule, constructorMethodInfo, out var constructorMethodRef))
                return false;

            constructorILProcessor.Emit(OpCodes.Call, constructorMethodRef);
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Nop);
            constructorILProcessor.Emit(OpCodes.Ret);

            return true;
        }

        bool InjectPushOfRPCParamters (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            MethodReference executionTarget,
            ParameterDefinition bufferPosParamDef,
            ref Instruction afterInstruction)
        {
            foreach (var paramDef in executionTarget.Parameters)
            {
                if (cecilUtils.ParameterIsString(executionTarget.Module, paramDef))
                {
                    if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseStringMarker>(typeof(RPCBufferIO), out var parseStringMethod))
                        return false;

                    if (!cecilUtils.TryImport(moduleDef, parseStringMethod, out var parseStringMethodRef))
                        return false;

                    cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !bufferPosParamDef.ParameterType.IsByReference);
                    cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, parseStringMethodRef);
                }

                else if (ParameterIsNativeArray(paramDef))
                {
                    var elementType = (paramDef.ParameterType as GenericInstanceType).GenericArguments[0];
                    if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseNativeArrayMarker>(typeof(RPCBufferIO), out var parseNativeArrayMethodInfo))
                        return false;

                    if (!cecilUtils.TryImport(moduleDef, parseNativeArrayMethodInfo, out var parseNativeArrayMethodRef))
                        return false;

                    var genericInstanceMethod = new GenericInstanceMethod(parseNativeArrayMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = cecilUtils.Import(moduleDef, elementType);
                    genericInstanceMethod.GenericArguments.Add(paramRef);

                    cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !bufferPosParamDef.ParameterType.IsByReference);
                    cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, cecilUtils.Import(moduleDef, genericInstanceMethod));
                }

                else if (paramDef.ParameterType.IsArray)
                {
                    var elementType = paramDef.ParameterType.GetElementType();
                    if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseArrayMarker>(typeof(RPCBufferIO), out var parseArrayMethod))
                        return false;

                    if (!cecilUtils.TryImport(moduleDef, parseArrayMethod, out var parseArrayMethodRef))
                        return false;

                    var genericInstanceMethod = new GenericInstanceMethod(parseArrayMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = cecilUtils.Import(moduleDef, elementType);
                    genericInstanceMethod.GenericArguments.Add(paramRef);

                    cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !bufferPosParamDef.ParameterType.IsByReference);
                    cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, cecilUtils.Import(moduleDef, genericInstanceMethod));
                }
                
                // For char types, we need to handle them as a special case to respect UTF-16. However, for chars embedded in
                // structs, you will need to use [MarshalAsAttribute(UnmanagedType.U2)] to explicitly describe to Marshal that
                // a char is 2 bytes instead of 1. Before changing this, you should inspect to matching char to byte in:
                // "TryInjectBridgeToDynamicallySizedRPCPropagation".
                else if (paramDef.ParameterType == moduleDef.TypeSystem.Char)
                {
                    // There is a specific method for converting bytes from the received RPC buffer to char.
                    if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseCharMarker>(typeof(RPCBufferIO), out var parseMethodInfo))
                        return false;
                    
                    if (!cecilUtils.TryImport(moduleDef, parseMethodInfo, out var parseMethodRef))
                        return false;

                    cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !bufferPosParamDef.ParameterType.IsByReference);
                    cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, parseMethodRef);  // Call generic method to convert bytes into our struct.
                }

                else if (paramDef.ParameterType.IsValueType)
                {
                    if (!cecilUtils.TryFindMethodWithAttribute<RPCBufferIO.ParseStructureMarker>(typeof(RPCBufferIO), out var parseMethodInfo))
                        return false;
                    
                    if (!cecilUtils.TryImport(moduleDef, parseMethodInfo, out var parseMethodRef))
                        return false;
                    
                    var genericInstanceMethod = new GenericInstanceMethod(parseMethodRef); // Create a generic method of RPCEmitter.ParseStructure.
                    var paramRef = cecilUtils.Import(moduleDef, paramDef.ParameterType);
                    genericInstanceMethod.GenericArguments.Add(paramRef);
                    var genericInstanceMethodRef = cecilUtils.Import(moduleDef, genericInstanceMethod);

                    cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, bufferPosParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: !bufferPosParamDef.ParameterType.IsByReference);
                    cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, genericInstanceMethodRef);  // Call generic method to convert bytes into our struct.
                }
            }

            return true;
        }

        bool TryInjectInstanceRPCExecution (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            Instruction beforeInstruction,
            MethodReference executionTarget,
            out Instruction firstInstructionOfInjection)
        {
            if (beforeInstruction == null)
            {
                logger.LogError("Unable to inject instance RPC execution IL isntructions, the point at which we want to inject instructions before is null!");
                firstInstructionOfInjection = null;
                return false;
            }

            var method = ilProcessor.Body.Method;
            if (!cecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.PipeIdMarker>(method, out var pipeIdParamDef) ||
                !cecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            if (!TryGetGetInstanceMethodRef(moduleDef, out var getInstanceMEthodRef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            var afterInstruction = firstInstructionOfInjection = cecilUtils.InsertPushParameterToStackBefore(ilProcessor, beforeInstruction, pipeIdParamDef, isStaticCaller: method.IsStatic, byReference: false); // Load pipeId parameter onto stack.
            cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, getInstanceMEthodRef);

            if (!executionTarget.HasParameters)
            {
                // Call method on target object without any parameters.
                cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, executionTarget);

                cecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            InjectPushOfRPCParamters(
                moduleDef,
                ilProcessor,
                executionTarget,
                 bufferPosParamDef,
                 ref afterInstruction);

            cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, executionTarget);
            cecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);
            cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
            return true;
        }

        bool TryInjectStaticRPCExecution (
            ModuleDefinition moduleDef,
            ILProcessor ilProcessor,
            Instruction beforeInstruction,
            MethodReference targetMethodRef,
            out Instruction firstInstructionOfInjection)
        {
            var method = ilProcessor.Body.Method;
            if (!cecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCBufferPositionMarker>(method, out var bufferPosParamDef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            Instruction afterInstruction = null;

            if (!cecilUtils.TryImport(ilProcessor.Body.Method.Module, typeof(void), out var voidTypeRef))
            {
                firstInstructionOfInjection = null;
                return false;
            }

            if (!targetMethodRef.HasParameters)
            {
                // Call method on target object without any parameters.
                afterInstruction = firstInstructionOfInjection = cecilUtils.InsertCallBefore(ilProcessor, beforeInstruction, targetMethodRef);

                if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                    cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

                cecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

                cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
                return true;
            }

            afterInstruction = firstInstructionOfInjection = cecilUtils.InsertBefore(ilProcessor, beforeInstruction, OpCodes.Nop);

            InjectPushOfRPCParamters(
                moduleDef,
                ilProcessor,
                targetMethodRef,
                 bufferPosParamDef,
                 ref afterInstruction);

            cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, targetMethodRef);

            if (targetMethodRef.ReturnType.Module.Name != voidTypeRef.Module.Name || targetMethodRef.ReturnType.FullName != voidTypeRef.FullName)
                cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Pop);

            cecilUtils.InsertPushIntAfter(ilProcessor, ref afterInstruction, 1);

            cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Ret);
            return true;
        }

        bool TryInjectSwitchCaseForRPC (
            ILProcessor ilProcessor,
            string valueToPushForBeq,
            Instruction jmpToInstruction,
            ref Instruction afterInstruction)
        {
            if (afterInstruction == null)
            {
                logger.LogError("Unable to inject switch jump instructions, the instruction we want to inject AFTER is null!");
                return false;
            }

            if (jmpToInstruction == null)
            {
                logger.LogError("Unable to inject switch jump instructions, the target instruction to jump to is null!");
                return false;
            }

            if (!cecilUtils.TryFindParameterWithAttribute<RPCInterfaceRegistry.RPCHashMarker>(ilProcessor.Body.Method, out var rpcHashParamDef))
            {
                return false;
            }

            cecilUtils.InsertPushParameterToStackAfter(ilProcessor, ref afterInstruction, rpcHashParamDef, isStaticCaller: ilProcessor.Body.Method.IsStatic, byReference: false);

            cecilUtils.InsertPushStringAfter(ilProcessor, ref afterInstruction, valueToPushForBeq);
            
            var opEqualityMethodInfo = typeof(string).GetMethod("op_Equality");
            if (!cecilUtils.TryImport(ilProcessor.Body.Method.Module, opEqualityMethodInfo, out var opEqualityMethodRef))
            {
                return false;
            }
            
            cecilUtils.InsertCallAfter(ilProcessor, ref afterInstruction, opEqualityMethodRef);
            cecilUtils.InsertAfter(ilProcessor, ref afterInstruction, OpCodes.Brtrue, jmpToInstruction);

            return true;
        }

        bool TryGetTypeSystemPrimitiveFromAlias (AssemblyDefinition compiledAssemblyDef, out TypeReference primitiveTypeReference)
        {
            var buintType = typeof(buint);
            if (buintType.Name == compiledAssemblyDef.MainModule.TypeSystem.UInt16.Name)
            {
                primitiveTypeReference = compiledAssemblyDef.MainModule.TypeSystem.UInt16;
                return true;
            }

            else if (buintType.Name == compiledAssemblyDef.MainModule.TypeSystem.UInt32.Name)
            {
                primitiveTypeReference = compiledAssemblyDef.MainModule.TypeSystem.UInt32;
                return true;
            }

            primitiveTypeReference = null;
            return false;
        }

        ParameterDefinition NewRPCHashParameter (AssemblyDefinition compiledAssemblyDef)
        {
            var paramDef = new ParameterDefinition("rpcHash", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.String);
            cecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCHashMarker>(compiledAssemblyDef, paramDef);
            return paramDef;
        }

        ParameterDefinition NewPipeIdParameter (AssemblyDefinition compiledAssemblyDef)
        {
            var paramDef = new ParameterDefinition("pipeId", Mono.Cecil.ParameterAttributes.None, compiledAssemblyDef.MainModule.TypeSystem.UInt16);
            cecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.PipeIdMarker>(compiledAssemblyDef, paramDef);
            return paramDef;
        }

        ParameterDefinition NewParameterPayloadSizeParameter (AssemblyDefinition compiledAssemblyDef, TypeReference bufferIndexTypeRef)
        {
            var paramDef = new ParameterDefinition("parametersPayloadSize", Mono.Cecil.ParameterAttributes.None, bufferIndexTypeRef);
            cecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.ParametersPayloadSizeMarker>(compiledAssemblyDef, paramDef);
            return paramDef;
        }

        ParameterDefinition NewPCBufferParameterPosition (AssemblyDefinition compiledAssemblyDef, TypeReference bufferIndexTypeRef)
        {
            var paramDef = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.None, bufferIndexTypeRef);
            cecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, paramDef);
            return paramDef;
        }

        ParameterDefinition NewPCBufferParameterPositionRef (AssemblyDefinition compiledAssemblyDef, TypeReference bufferIndexTypeRef)
        {
            var paramDef = new ParameterDefinition("rpcBufferParameterPosition", Mono.Cecil.ParameterAttributes.None, new ByReferenceType(bufferIndexTypeRef));
            cecilUtils.AddCustomAttributeToParameter<RPCInterfaceRegistry.RPCBufferPositionMarker>(compiledAssemblyDef, paramDef);
            return paramDef;
        }

        bool TryGenerateRPCILTypeInCompiledAssembly (AssemblyDefinition compiledAssemblyDef, out TypeReference rpcInterfaceRegistryDerrivedTypeRef)
        {
            var newTypeDef = new TypeDefinition(RPCILGenerator.GeneratedRPCILNamespace, RPCILGenerator.GeneratedRPCILTypeName, Mono.Cecil.TypeAttributes.NestedPrivate);

            TypeReference baseTypeRef = null;
            if (!cecilUtils.TryImport(compiledAssemblyDef.MainModule, typeof(RPCInterfaceRegistry), out baseTypeRef))
            {
                rpcInterfaceRegistryDerrivedTypeRef = null;
                return false;
            }

            newTypeDef.BaseType = baseTypeRef;

            var rpcHashParameterDef = NewRPCHashParameter(compiledAssemblyDef);
            var pipeParameterDef = NewPipeIdParameter(compiledAssemblyDef);

            if (!TryGetTypeSystemPrimitiveFromAlias(compiledAssemblyDef, out var bufferIndexTypeRef))
            {
                rpcInterfaceRegistryDerrivedTypeRef = null;
                return false;
            }

            var parametersPayloadSize = NewParameterPayloadSizeParameter(compiledAssemblyDef, bufferIndexTypeRef);
            var rpcBufferParameterPosition = NewPCBufferParameterPosition(compiledAssemblyDef, bufferIndexTypeRef);

            var onTryCallInstanceMethodDef = new MethodDefinition("OnTryCallInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryCallInstanceMethodDef.Parameters.Add(rpcHashParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(pipeParameterDef);
            onTryCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPosition);
            cecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.OnTryCallInstanceImplementationAttribute>(compiledAssemblyDef.MainModule, onTryCallInstanceMethodDef);
            var il = onTryCallInstanceMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            rpcHashParameterDef = NewRPCHashParameter(compiledAssemblyDef);
            parametersPayloadSize = NewParameterPayloadSizeParameter(compiledAssemblyDef, bufferIndexTypeRef);
            rpcBufferParameterPosition = NewPCBufferParameterPosition(compiledAssemblyDef, bufferIndexTypeRef);

            var onTryStaticCallInstanceMethodDef = new MethodDefinition("OnTryCallStatic", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcHashParameterDef);
            onTryStaticCallInstanceMethodDef.Parameters.Add(parametersPayloadSize);
            onTryStaticCallInstanceMethodDef.Parameters.Add(rpcBufferParameterPosition);
            cecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.OnTryCallStaticImplementationAttribute>(compiledAssemblyDef.MainModule, onTryStaticCallInstanceMethodDef);
            il = onTryStaticCallInstanceMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            rpcHashParameterDef = NewRPCHashParameter(compiledAssemblyDef);
            pipeParameterDef = NewPipeIdParameter(compiledAssemblyDef);
            parametersPayloadSize = NewParameterPayloadSizeParameter(compiledAssemblyDef, bufferIndexTypeRef);
            rpcBufferParameterPosition = NewPCBufferParameterPosition(compiledAssemblyDef, bufferIndexTypeRef);

            var executeQueuedRPCMethodDef = new MethodDefinition("OnTryCallQueuedInstance", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static, compiledAssemblyDef.MainModule.TypeSystem.Boolean);
            executeQueuedRPCMethodDef.Parameters.Add(rpcHashParameterDef);
            executeQueuedRPCMethodDef.Parameters.Add(pipeParameterDef);
            executeQueuedRPCMethodDef.Parameters.Add(parametersPayloadSize);
            executeQueuedRPCMethodDef.Parameters.Add(rpcBufferParameterPosition);
            cecilUtils.AddCustomAttributeToMethod<RPCInterfaceRegistry.OnTryCallQueuedInstanceImplementationAttribute>(compiledAssemblyDef.MainModule, executeQueuedRPCMethodDef);
            il = executeQueuedRPCMethodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldc_I4_0);
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
