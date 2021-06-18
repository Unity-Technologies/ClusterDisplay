using System;
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
        private static bool TryDetermineSizeOfPrimitive (string typeName, ref int size)
        {
            switch (typeName)
            {
                case "Byte":
                case "SByte":
                case "Boolean":
                    size += 1;
                    return true;
                case "Int16":
                case "UInt16":
                case "Char":
                    size += 2;
                    return true;
                case "Int32":
                case "UInt32":
                case "Single":
                    size += 4;
                    return true;
                case "Int64":
                case "UInt64":
                case "Double":
                    size += 8;
                    return true;
                default:
                    Debug.LogError($"Unable to determine size of assumed primitive type: \"{typeName}\".");
                    return false;
            }
        }

        private static bool TryDetermineSizeOfStruct (TypeDefinition typeDefinition, ref int size)
        {
            bool allValid = true;

            foreach (var field in typeDefinition.Fields)
            {
                if (field.IsStatic)
                    continue;
                allValid &= TryDetermineSizeOfValueType(field.FieldType.Resolve(), ref size);
            }

            return allValid;
        }

        private static bool TryDetermineSizeOfValueType (TypeDefinition typeDefinition, ref int size)
        {
            if (typeDefinition.IsPrimitive || typeDefinition.IsEnum)
                return TryDetermineSizeOfPrimitive(typeDefinition.Name, ref size);
            else if (typeDefinition.IsValueType)
                return TryDetermineSizeOfStruct(typeDefinition, ref size);

            Debug.LogError($"Unable to determine size of supposed value type: \"{typeDefinition.FullName}\".");
            return false;
        }

        private static Instruction PushParameterToStack (ParameterDefinition parameterDefinition, bool isStaticCaller, bool byReference)
        {
            if (byReference)
                return Instruction.Create(OpCodes.Ldarga, parameterDefinition);

            if (isStaticCaller)
            {
                switch (parameterDefinition.Index)
                {
                    case 0:
                        return Instruction.Create(OpCodes.Ldarg_0);
                    case 1:
                        return Instruction.Create(OpCodes.Ldarg_1);
                    case 2:
                        return Instruction.Create(OpCodes.Ldarg_2);
                    case 3:
                        return Instruction.Create(OpCodes.Ldarg_3);
                    default:
                        return Instruction.Create(OpCodes.Ldarg, parameterDefinition);
                }
            }

            switch (parameterDefinition.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Ldarg_1);
                case 1:
                    return Instruction.Create(OpCodes.Ldarg_2);
                case 2:
                    return Instruction.Create(OpCodes.Ldarg_3);
                default:
                    return Instruction.Create(OpCodes.Ldarg_S, parameterDefinition);
            }
        }

        private static string GetAssemblyLocation (AssemblyNameReference name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name).Location;
        private static string GetAssemblyLocation (string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name).Location;

        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }

        private static bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
        {
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(),
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                ReadingMode = ReadingMode.Immediate,
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            try
            {
                assemblyDef = AssemblyDefinition.ReadAssembly(peStream, readerParameters);
                return true;
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                assemblyDef = null;
                return false;
            }
        }

        private static bool TryGetMethodReference (
            ModuleDefinition moduleDef,
            TypeDefinition typeDef, 
            ref SerializedRPC inRPCTokenizer, 
            out MethodReference methodRef)
        {
            var rpcTokenizer = inRPCTokenizer;
            MethodDefinition methodDef = null;
            bool found = (methodDef = typeDef.Methods.Where(method =>
            {
                // Debug.Log($"Method Signature: {methodDef.Name} == {rpcTokenizer.MethodName} &&\n{methodDef.ReturnType.Resolve().Module.Assembly.Name.Name} == {rpcTokenizer.DeclaringReturnTypeAssemblyName} &&\n{methodDef.HasParameters} == {rpcTokenizer.ParameterCount > 0} &&\n{methodDef.Parameters.Count} == {rpcTokenizer.ParameterCount}");
                bool allMatch = 
                    method.HasParameters == rpcTokenizer.ParameterCount > 0 &&
                    method.Parameters.Count == rpcTokenizer.ParameterCount &&
                    method.Name == rpcTokenizer.methodName &&
                    method.ReturnType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.declaringReturnTypeAssemblyName &&
                    method.Parameters.All(parameterDefinition =>
                    {
                        if (rpcTokenizer.ParameterCount == 0)
                            return true;

                        bool any = false;
                        for (int i = 0; i < rpcTokenizer.ParameterCount; i++)
                        {
                            // Debug.Log($"Method Parameters: {parameterDefinition.Name} == {rpcTokenizer[i].parameterName} &&\n{parameterDefinition.ParameterType.FullName} == {rpcTokenizer[i].parameterTypeFullName} &&\n{parameterDefinition.ParameterType.Module.Assembly.Name.Name} == {rpcTokenizer[i].declaringParameterTypeAssemblyName}");
                            any |=
                                parameterDefinition.Name == rpcTokenizer[i].parameterName &&
                                parameterDefinition.ParameterType.FullName == rpcTokenizer[i].parameterTypeFullName &&
                                parameterDefinition.ParameterType.Resolve().Module.Assembly.Name.Name == rpcTokenizer[i].declaringParameterTypeAssemblyName;
                        }
                        return any;
                    });

                return allMatch;

            }).FirstOrDefault()) != null;

            if (!found)
            {
                Debug.LogError($"Unable to find method reference for serialized RPC: \"{inRPCTokenizer.methodName}\" declared in: \"{typeDef.FullName}\".");
                methodRef = null;
                return false;
            }

            methodRef = moduleDef.ImportReference(methodDef);
            return true;
        }

        private static bool TryFindNestedTypeWithAttribute<T> (ModuleDefinition moduleDef, Type type, out TypeReference typeRef, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var typeDef = nestedTypes.FirstOrDefault(nestedType => nestedType.GetCustomAttribute<T>() != null);

            if (typeDef == null)
            {
                typeRef = null;
                Debug.LogError($"Unable to find nested type with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");
                return false;
            }

            typeRef = moduleDef.ImportReference(typeDef);
            return true;
        }

        private static bool TryFindMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var methods = type.GetMethods(bindingFlags);
            var found = (methodInfo = methods.FirstOrDefault(method => method.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                Debug.LogError($"Unable to find method info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        private static bool TryFindFieldWithAttribute<T> (System.Type type, out FieldInfo fieldInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var fields = type.GetFields(bindingFlags);
            var found = (fieldInfo = fields.FirstOrDefault(field => field.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                Debug.LogError($"Unable to find field info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        private static bool TryFindPropertyGetMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
        {
            methodInfo = null;
            var attributeType = typeof(T);
            var propertyInfo = type.GetProperties()
                .Where(pi => pi.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault();

            if (propertyInfo == null || (methodInfo = propertyInfo.GetGetMethod()) == null)
                Debug.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return methodInfo != null;
        }

        private static bool TryFindFieldDefinitionWithAttribute<T> (TypeDefinition typeDef, out FieldDefinition fieldDefinition) where T : Attribute
        {
            fieldDefinition = null;
            var attributeType = typeDef.Module.ImportReference(typeof(T));
            bool found = (fieldDefinition = typeDef.Fields
                .Where(field => field.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{typeDef.FullName}\".");

            return found;
        }

        private static bool TryFindParameterWithAttribute<T> (
            MethodDefinition methodDefinition, 
            out ParameterDefinition parameterDef)
        {
            var parameterAttributeType = methodDefinition.Module.ImportReference(typeof(T));
            bool found = (parameterDef = methodDefinition
                .Parameters
                .Where(parameter => parameter.CustomAttributes.Any(customAttributeData => customAttributeData.AttributeType.FullName == parameterAttributeType.FullName))
                .FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find parameter with attribute: \"{typeof(T).FullName}\" in method: \"{methodDefinition.Name}\" in type: \"{methodDefinition.DeclaringType.FullName}\".");

            return found;
        }

        private static bool TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<T> (CustomAttribute customAttribute, out int customAttributeArgumentIndex)
        {
            var customAttributeArgumentAttributeType = customAttribute.AttributeType.Module.ImportReference(typeof(T));
            var constructorMethodDef = customAttribute.Constructor.Resolve();

            for (int i = 0; i < customAttribute.Constructor.Parameters.Count; i++)
            {
                var parameterDef = constructorMethodDef.Parameters[i].Resolve();
                if (!parameterDef.CustomAttributes.Any(parameterCustomAttribute => parameterCustomAttribute.AttributeType.FullName == customAttributeArgumentAttributeType.FullName))
                    continue;

                customAttributeArgumentIndex = i;
                return true;
            }

            Debug.LogError($"Unable to find index of custom attribute constructor argument with attribute: \"{typeof(T).FullName}\".");
            customAttributeArgumentIndex = -1;
            return false;
        }

        private static bool TryGetCachedGetIsMasterMarkerMethod (out MethodInfo getIsMasterMethod)
        {
            if (cachedGetIsMasterMethod == null && !TryFindPropertyGetMethodWithAttribute<ClusterDisplayState.IsMasterMarker>(typeof(ClusterDisplayState), out cachedGetIsMasterMethod))
            {
                getIsMasterMethod = null;
                return false;
            }

            getIsMasterMethod = cachedGetIsMasterMethod;
            return true;
        }

        private static bool ParameterIsString (ModuleDefinition moduleDefinition, ParameterDefinition parameterDef)
        {
            if (cachedStringTypeRef == null)
                cachedStringTypeRef = moduleDefinition.ImportReference(typeof(string));

            return 
                parameterDef.ParameterType.Namespace == cachedStringTypeRef.Namespace && 
                parameterDef.ParameterType.Name == cachedStringTypeRef.Name;
        }

        private static Instruction PushInt (int value)
        {
            switch (value)
            {
                case 0:
                    return Instruction.Create(OpCodes.Ldc_I4_0);
                case 1:
                    return Instruction.Create(OpCodes.Ldc_I4_1);
                case 2:
                    return Instruction.Create(OpCodes.Ldc_I4_2);
                case 3:
                    return Instruction.Create(OpCodes.Ldc_I4_3);
                case 4:
                    return Instruction.Create(OpCodes.Ldc_I4_4);
                case 5:
                    return Instruction.Create(OpCodes.Ldc_I4_5);
                case 6:
                    return Instruction.Create(OpCodes.Ldc_I4_6);
                case 7:
                    return Instruction.Create(OpCodes.Ldc_I4_7);
                case 8:
                    return Instruction.Create(OpCodes.Ldc_I4_8);

                default:
                    return Instruction.Create(OpCodes.Ldc_I4, value);
            }
        }

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

            if (afterInstruction == null)
            {
                Debug.LogError($"Unable to inject call to: \"{call.Name}\" declared in: \"{call.DeclaringType.FullName}\", the instruction to inject after is null!");
                lastInstruction = null;
                return false;
            }

            if (isStatic)
                InsertPushIntAfter(il, ref afterInstruction, rpcId);

            else
            {
                InsertPushThisAfter(il, ref afterInstruction);
                InsertPushIntAfter(il, ref afterInstruction, rpcId);
            }

            InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            // InsertPushIntAfter(il, ref afterInstruction, ((RPCExecutionStage)rpcExecutionStage) != RPCExecutionStage.Automatic ? 1 : 0);
            InsertPushIntAfter(il, ref afterInstruction, sizeOfAllParameters);
            InsertCallAfter(il, ref afterInstruction, call);
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
            var debugTypeRef = moduleDef.ImportReference(debugType);
            var debugTypeDef = debugTypeRef.Resolve();
            var logMethodRef = moduleDef.ImportReference(debugTypeDef.Methods.Where(method => {
                return
                    method.Name == "Log" &&
                    method.Parameters.Count == 1;
            }).FirstOrDefault());

            lastInstruction = null;

            InsertPushStringAfter(il, ref afterInstruction, message);
            InsertCallAfter(il, ref afterInstruction, logMethodRef);
        }

        private bool TryFindMethodWithMatchingFormalySerializedAs (
            ModuleDefinition moduleDef, 
            TypeDefinition typeDefinition, 
            string serializedMethodName, 
            out MethodReference outMethodRef)
        {
            var rpcMethodAttributeType = typeof(RPC);
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

                        outMethodRef = moduleDef.ImportReference(methodDef);
                        Debug.LogFormat($"Found renamed method: \"{outMethodRef.Name}\" that was previously named as: \"{serializedMethodName}\".");
                        return true;
                    }
                }
            }

            outMethodRef = null;
            return false;
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
            if (!TryFindMethodWithAttribute<RPCEmitter.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendRPCValueTypeParameterValueMethodInfo))
                return false;

            if (!TryFindMethodWithAttribute<RPCEmitter.AppendRPCStringParameterValueMarker>(rpcEmitterType, out var appendRPCStringParameterValueMethodInfo))
                return false;

            if (!TryFindMethodWithAttribute<RPCEmitter.AppendRPCArrayParameterValueMarker>(rpcEmitterType, out var appendRPCArrayParameterValueMethodInfo))
                return false;

            var appendRPCValueTypeParameterValueMethodRef = targetMethodRef.Module.ImportReference(appendRPCValueTypeParameterValueMethodInfo);
            var appendRPCStringParameterValueMethodRef = targetMethodRef.Module.ImportReference(appendRPCStringParameterValueMethodInfo);
            var appendRPCArrayParameterValueMethodRef = targetMethodRef.Module.ImportReference(appendRPCArrayParameterValueMethodInfo);

            var targetMethodDef = targetMethodRef.Resolve();

            if (targetMethodDef.IsStatic)
                InsertPushIntAfter(il, ref afterInstruction, rpcId);

            else
            {
                InsertPushThisAfter(il, ref afterInstruction);
                InsertPushIntAfter(il, ref afterInstruction, rpcId);
            }

            InsertPushIntAfter(il, ref afterInstruction, rpcExecutionStage);
            // InsertPushIntAfter(il, ref afterInstruction, ((RPCExecutionStage)rpcExecutionStage) != RPCExecutionStage.Automatic ? 1 : 0);
            InsertPushIntAfter(il, ref afterInstruction, totalSizeOfStaticallySizedRPCParameters);

            if (targetMethodRef.HasParameters)
            {
                // Loop through each array/string parameter adding the runtime byte size to total parameters payload size.
                foreach (var param in targetMethodRef.Parameters)
                {
                    if (ParameterIsString(targetMethodRef.Module, param))
                    {
                        InsertPushIntAfter(il, ref afterInstruction, 1);
                        InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: targetMethodDef.IsStatic, byReference: false);

                        var stringTypeDef = cachedStringTypeRef.Resolve();
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

                        var stringLengthGetterMethodRef = targetMethodRef.Module.ImportReference(stringLengthGetterMethodDef); // Get the string length getter method.
                        InsertCallAfter(il, ref afterInstruction, stringLengthGetterMethodRef); // Call string length getter with pushes the string length to the stack.
                        InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply char size of one byte by the length of the string.
                        InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add string size in bytes to total parameters payload size.
                        InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_2); // Load "2" as a constant which we designate as the array's byte size.
                        InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                    }

                    else if (param.ParameterType.IsArray)
                    {
                        int arrayElementSize = 0;
                        if (!TryDetermineSizeOfValueType(param.ParameterType.Resolve(), ref arrayElementSize))
                            return false;

                        InsertPushIntAfter(il, ref afterInstruction, arrayElementSize); // Push array element size to stack.
                        InsertPushParameterToStackAfter(il, ref afterInstruction, param, isStaticCaller: targetMethodDef.IsStatic, byReference: false); // Push the array reference parameter to the stack.

                        var arrayTypeRef = targetMethodRef.Module.ImportReference(typeof(Array));
                        var arrayTypeDef = arrayTypeRef.Resolve();
                        var arrayLengthPropertyRef = arrayTypeDef.Properties.FirstOrDefault(propertyDef =>
                        {
                            return
                                propertyDef.GetMethod != null &&
                                propertyDef.Name == "Length";
                        });

                        if (arrayLengthPropertyRef == null)
                        {
                            Debug.LogError($"Unable to find Length property for parameter type: \"{param.ParameterType.FullName}\".");
                            return false;
                        }

                        var arrayLengthGetterMethodDef = arrayLengthPropertyRef.GetMethod;
                        if (arrayLengthGetterMethodDef == null)
                        {
                            Debug.LogError($"Unable to find Length property getter method for parameter type: \"{param.ParameterType.FullName}\".");
                            return false;
                        }

                        var arrayLengthGetterMethodRef = targetMethodRef.Module.ImportReference(arrayLengthGetterMethodDef); // Find array Length get property.

                        InsertCallAfter(il, ref afterInstruction, arrayLengthGetterMethodRef); // Call array length getter which will push array length to stack.
                        InsertAfter(il, ref afterInstruction, OpCodes.Mul); // Multiply array element size by array length.
                        InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add total array size in bytes to total parameters payload size.
                        InsertAfter(il, ref afterInstruction, OpCodes.Ldc_I4_2); // Load "2" as a constant which we designate as the array's byte size.
                        InsertAfter(il, ref afterInstruction, OpCodes.Add); // Add the constant to the total parameters payload size.
                    }
                }
            }

            InsertCallAfter(il, ref afterInstruction, appendRPCMethodRef);

            if (targetMethodRef.HasParameters)
            {
                // Loop through the parameters again to inject instructions to push each parameter values to RPC buffer.
                foreach (var param in targetMethodRef.Parameters)
                {
                    GenericInstanceMethod genericInstanceMethod = null;
                    var paramDef = param.Resolve();

                    if (ParameterIsString(targetMethodRef.Module, param))
                    {
                        InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                        InsertCallAfter(il, ref afterInstruction, appendRPCStringParameterValueMethodRef);
                        continue;
                    }

                    if (param.ParameterType.IsArray)
                    {
                        genericInstanceMethod = new GenericInstanceMethod(appendRPCArrayParameterValueMethodRef);
                        genericInstanceMethod.GenericArguments.Add(param.ParameterType.GetElementType());
                    }

                    else
                    {
                        genericInstanceMethod = new GenericInstanceMethod(appendRPCValueTypeParameterValueMethodRef);
                        genericInstanceMethod.GenericArguments.Add(param.ParameterType);
                    }

                    InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, byReference: paramDef.IsOut || paramDef.IsIn);
                    InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
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
            if (!TryFindMethodWithAttribute<RPCEmitter.AppendRPCValueTypeParameterValueMarker>(rpcEmitterType, out var appendRPCValueTypeParameterValueMethodInfo))
                return false;

            var targetMethodDef = targetMethodRef.Resolve();

            var appendRPCValueTypeParameterValueMethodRef = targetMethodRef.Module.ImportReference(appendRPCValueTypeParameterValueMethodInfo);

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

                    InsertPushParameterToStackAfter(il, ref afterInstruction, paramDef, isStaticCaller: targetMethodDef.IsStatic, paramDef.IsOut || paramDef.IsIn);
                    InsertCallAfter(il, ref afterInstruction, genericInstanceMethod);
                }
            }

            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));

            return true;
        }

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
                var typeReference = moduleDef.ImportReference(param.ParameterType);

                if (typeReference.IsValueType)
                {
                    int sizeOfType = 0;
                    if (!TryDetermineSizeOfValueType(typeReference.Resolve(), ref sizeOfType))
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

                else hasDynamicallySizedRPCParameters =
                        ParameterIsString(moduleDef, param) ||
                        typeReference.IsArray;
            }

            return true;
        }

        private static bool TryImportTryGetSingletonObject<T> (
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

            derrivedTypeRef = assemblyDef.MainModule.ImportReference(derrivedTypeDef);
            return true;
        }
    }
}
