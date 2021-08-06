using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public static class CecilUtils
    {
        public static bool MethodDefinitionMatchesMethod(MethodDefinition methodDef, ConstructorInfo methodInfo)
        {
            if (methodDef.Name != methodInfo.Name)
                return false;

            if (methodDef.DeclaringType.Namespace != methodInfo.DeclaringType.Namespace ||
                methodDef.DeclaringType.Name != methodInfo.DeclaringType.Name)
                return false;

            var parameters = methodInfo.GetParameters();
            if (methodDef.Parameters.Count != parameters.Length)
                return false;

            if (parameters.Length == 0)
                return true;

            bool allMatch = true;
            for (int pi = 0; pi < parameters.Length; pi++)
            {
                allMatch &=
                    methodDef.Parameters[pi].ParameterType.Name == parameters[pi].ParameterType.Name &&
                    methodDef.Parameters[pi].Name == parameters[pi].Name;
            }

            return allMatch;
        }


        public static bool MethodDefinitionMatchesMethod(MethodDefinition methodDef, MethodInfo methodInfo)
        {
            if (methodDef.Name != methodInfo.Name)
                return false;

            if (methodDef.DeclaringType.Namespace != methodInfo.DeclaringType.Namespace ||
                methodDef.DeclaringType.Name != methodInfo.DeclaringType.Name)
                return false;

            var parameters = methodInfo.GetParameters();
            if (methodDef.Parameters.Count != parameters.Length)
                return false;

            if (parameters.Length == 0)
                return true;

            bool allMatch = true;
            for (int pi = 0; pi < parameters.Length; pi++)
            {
                allMatch &=
                    methodDef.Parameters[pi].ParameterType.Name == parameters[pi].ParameterType.Name &&
                    methodDef.Parameters[pi].Name == parameters[pi].Name;
            }

            return allMatch;
        }

        public static bool TryFindMatchingMethodDefinition (TypeDefinition typeDef, MethodInfo methodInfo, out MethodDefinition methodDef)
        {
            methodDef = typeDef.Methods.FirstOrDefault(methodDefinition => MethodDefinitionMatchesMethod(methodDefinition, methodInfo));
            if (methodDef == null)
                Debug.LogError($"Unable to find {nameof(MethodDefinition)} for method: \"{methodInfo.Name}\" in type: \"{typeDef.Namespace}.{typeDef.Name}\".");
            return true;
        }

        public static bool TryFindMatchingMethodDefinition (TypeDefinition typeDef, ConstructorInfo constructorInfo, out MethodDefinition methodDef)
        {
            methodDef = typeDef.Methods.FirstOrDefault(methodDefinition => MethodDefinitionMatchesMethod(methodDefinition, constructorInfo));
            if (methodDef == null)
                Debug.LogError($"Unable to find {nameof(MethodDefinition)} for constructor: \"{constructorInfo.Name}\" in type: \"{typeDef.Namespace}.{typeDef.Name}\".");
            return true;
        }

        private static bool RecursivelyTryFindNestedType (TypeDefinition typeDef, Type type, out TypeDefinition nestedTypeDef)
        {
            for (int nti = 0; nti < typeDef.NestedTypes.Count; nti++)
            {
                if (typeDef.Namespace != type.DeclaringType.Namespace ||
                    typeDef.Name != type.DeclaringType.Name ||
                    typeDef.NestedTypes[nti].Name != type.Name)
                {
                    if (!RecursivelyTryFindNestedType(typeDef.NestedTypes[nti], type, out nestedTypeDef))
                        continue;

                    return true;
                }

                nestedTypeDef = typeDef.NestedTypes[nti];
                return true;
            }

            nestedTypeDef = null;
            return false;
        }

        private static bool FindNestedType (ModuleDefinition moduleToSearch, Type type, out TypeDefinition nestedTypeDef)
        {
            for (int ti = 0; ti < moduleToSearch.Types.Count; ti++)
            {
                if (!RecursivelyTryFindNestedType(moduleToSearch.Types[ti], type, out nestedTypeDef))
                    continue;
                return true;
            }

            nestedTypeDef = null;
            return false;
        }

        public static bool TryImport (ModuleDefinition moduleToImportInto, Type type, out TypeReference importedTypeRef)
        {
            if (moduleToImportInto.Name != type.Module.Name)
            {
                importedTypeRef = moduleToImportInto.ImportReference(type);
                return true;
            }

            if (type.IsNested)
            {
                if (!FindNestedType(moduleToImportInto, type, out var nestedTypeDef))
                {
                    importedTypeRef = null;
                    return false;
                }

                importedTypeRef = nestedTypeDef;
                return true;
            }

            else
            {
                importedTypeRef = moduleToImportInto.GetType(type.Namespace, type.Name);
                if (importedTypeRef != null)
                    return true;
            }

            Debug.LogError($"Unable to find type definition to import for type: \"{type.Name}\".");
            importedTypeRef = null;
            return false;
        }

        public static bool TryImport (ModuleDefinition moduleToImportInto, ConstructorInfo methodInfo, out MethodReference importedMethodRef)
        {
            if (moduleToImportInto.Name != methodInfo.Module.Name)
            {
                importedMethodRef = moduleToImportInto.ImportReference(methodInfo);
                return true;
            }

            if (!TryImport(moduleToImportInto, methodInfo.DeclaringType, out var typeDef))
            {
                Debug.LogError($"Unable to import method: \"{methodInfo.Name}\" declared in: \"{methodInfo.DeclaringType.Name}\", cannot find declaring type reference.");
                importedMethodRef = null;
                return false;
            }

            if (!TryFindMatchingMethodDefinition(typeDef.Resolve(), methodInfo, out var matchingMethodDef))
            {
                Debug.LogError($"Unable to find matching method definition for method: \"{methodInfo.Name}\" declared in: \"{methodInfo.DeclaringType.Name}\" to import.");
                importedMethodRef = null;
                return false;
            }

            importedMethodRef = matchingMethodDef;
            return true;
        }

        public static bool TryImport (ModuleDefinition moduleToImportInto, MethodInfo methodToImport, out MethodReference importedMethodRef)
        {
            if (moduleToImportInto.Name != methodToImport.Module.Name)
            {
                importedMethodRef = moduleToImportInto.ImportReference(methodToImport);
                return true;
            }

            if (!TryImport(moduleToImportInto, methodToImport.DeclaringType, out var typeDef))
            {
                Debug.LogError($"Unable to import method: \"{methodToImport.Name}\" declared in: \"{methodToImport.DeclaringType.Name}\", cannot find declaring type reference.");
                importedMethodRef = null;
                return false;
            }

            if (!TryFindMatchingMethodDefinition(typeDef.Resolve(), methodToImport, out var matchingMethodDef))
            {
                Debug.LogError($"Unable to find matching method definition for method: \"{methodToImport.Name}\" declared in: \"{methodToImport.DeclaringType.Name}\" to import.");
                importedMethodRef = null;
                return false;
            }

            importedMethodRef = matchingMethodDef;
            return true;
        }

        public static TypeReference Import (ModuleDefinition moduleToImportInto, TypeReference typeToImport)
        {
            if (moduleToImportInto.Name != typeToImport.Module.Name)
                return moduleToImportInto.ImportReference(typeToImport);
            return typeToImport;
        }

        public static TypeReference Import (ModuleDefinition moduleToImportInto, TypeDefinition typeToImport)
        {
            if (moduleToImportInto.Name != typeToImport.Module.Name)
                return moduleToImportInto.ImportReference(typeToImport);
            return typeToImport;
        }

        public static MethodReference Import (ModuleDefinition moduleToImportInto, MethodReference methodToImport)
        {
            if (moduleToImportInto.Name != methodToImport.Module.Name)
                return moduleToImportInto.ImportReference(methodToImport);
            return methodToImport;
        }

        public static MethodReference Import (ModuleDefinition moduleToImportInto, MethodDefinition methodToImport)
        {
            if (moduleToImportInto.Name != methodToImport.Module.Name)
                return moduleToImportInto.ImportReference(methodToImport);
            return methodToImport;
        }

        public static void InsertCallAfter (ILProcessor il, ref Instruction afterInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertCallBefore (ILProcessor il, Instruction beforeInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void IsertPushLocalVariableAfter (ILProcessor il, ref Instruction afterInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction IsertPushLocalVariableBefore (ILProcessor il, Instruction beforeInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertPushParameterToStackAfter (ILProcessor il, ref Instruction afterInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertPushParameterToStackBefore (ILProcessor il, Instruction beforeInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertPushBufferUIntAfter (ILProcessor il, ref Instruction afterInstruction, buint integer)
        {
            var instruction = PushBUint(integer);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertPushBufferUIntBefore (ILProcessor il, Instruction beforeInstruction, buint integer)
        {
            var instruction = PushBUint(integer);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertPushIntAfter (ILProcessor il, ref Instruction afterInstruction, int integer)
        {
            var instruction = PushInt((int)integer);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertPushIntBefore (ILProcessor il, Instruction beforeInstruction, int integer)
        {
            var instruction = PushInt((int)integer);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertPushStringAfter (ILProcessor il, ref Instruction afterInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertPushStringBefore (ILProcessor il, Instruction beforeInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertPushThisAfter (ILProcessor il, ref Instruction afterInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertPushThisBefore (ILProcessor il, Instruction beforeInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public static Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public static bool MethodIsCoroutine (MethodDefinition methodDef)
        {
            if (!CecilUtils.TryImport(methodDef.Module, typeof(System.Collections.IEnumerator), out var typeRef))
                return false;

            return methodDef.ReturnType.MetadataToken == typeRef.MetadataToken;
        }

        public static bool TryPushMethodRef<DelegateMarker> (AssemblyDefinition compiledAssemblyDef, MethodReference methodRef, ILProcessor constructorILProcessor)
            where DelegateMarker : Attribute
        {
            constructorILProcessor.Emit(OpCodes.Ldnull);
            constructorILProcessor.Emit(OpCodes.Ldftn, methodRef);

            if (!TryFindNestedTypeWithAttribute<DelegateMarker>(compiledAssemblyDef.MainModule, typeof(RPCInterfaceRegistry), out var delegateTypeRef))
                return false;

            constructorILProcessor.Emit(OpCodes.Newobj, Import(compiledAssemblyDef.MainModule, delegateTypeRef.Resolve().Methods.FirstOrDefault(method => method.IsConstructor)));
            return true;
        }

        public static bool TryDetermineSizeOfPrimitive (string typeName, ref buint size)
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

        public static bool TryDetermineSizeOfStruct (TypeDefinition typeDefinition, ref buint size)
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

        public static bool TryDetermineSizeOfValueType (TypeDefinition typeDefinition, ref buint size)
        {
            if (typeDefinition.IsPrimitive)
                return TryDetermineSizeOfPrimitive(typeDefinition.Name, ref size);

            else if (typeDefinition.IsEnum)
            {
                var fieldDef = typeDefinition.Fields.FirstOrDefault(field => field.Name == "value__");
                return TryDetermineSizeOfPrimitive(fieldDef.FieldType.Name, ref size);
            }

            else if (typeDefinition.IsValueType)
            {
                return TryDetermineSizeOfStruct(typeDefinition, ref size);
            }

            Debug.LogError($"Unable to determine size of supposed value type: \"{typeDefinition.FullName}\".");
            return false;
        }

        public static Instruction PushParameterToStack (ParameterDefinition parameterDefinition, bool isStaticCaller, bool byReference)
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

        public static bool TryGetMethodReference (
            ModuleDefinition moduleDef,
            TypeDefinition typeDef, 
            ref SerializedRPC inRPCTokenizer, 
            out MethodReference methodRef)
        {
            var rpcTokenizer = inRPCTokenizer;
            MethodDefinition methodDef = null;
            bool found = (methodDef = typeDef.Methods.Where(method =>
            {
                if (method.Name != rpcTokenizer.method.methodName ||
                    method.ReturnType.Namespace != rpcTokenizer.method.returnTypeNamespace ||
                    method.ReturnType.Resolve().Module.Assembly.Name.Name != rpcTokenizer.method.declaringReturnTypeAssemblyName)
                    return false;

                if (method.HasParameters == (rpcTokenizer.method.ParameterCount > 0))
                {
                    if (method.Parameters.Count != rpcTokenizer.method.ParameterCount)
                        return false;

                    bool allParametersMatch = true;
                    for (int pi = 0; pi < method.Parameters.Count; pi++)
                    {
                        if (!(allParametersMatch &= method.Parameters[pi].Name == rpcTokenizer.method[pi].parameterName))
                            return false;

                        allParametersMatch &= 
                            method.Parameters[pi].ParameterType.Namespace == rpcTokenizer.method[pi].parameterTypeNamespace &&
                            method.Parameters[pi].ParameterType.Name == rpcTokenizer.method[pi].parameterTypeName &&
                            method.Parameters[pi].ParameterType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.method[pi].declaringParameterTypeAssemblyName;
                    }

                    return allParametersMatch;
                }

                return true;

            }).FirstOrDefault()) != null;

            if (!found)
            {
                Debug.LogError($"Unable to find method reference for serialized RPC: \"{inRPCTokenizer.method.methodName}\" declared in: \"{typeDef.FullName}\".");
                methodRef = null;
                return false;
            }

            methodRef = Import(moduleDef, methodDef);
            return true;
        }

        public static bool TryFindNestedTypeWithAttribute<T> (ModuleDefinition moduleDef, Type type, out TypeReference typeRef, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var typeDef = nestedTypes.FirstOrDefault(nestedType => nestedType.GetCustomAttribute<T>() != null);

            if (typeDef == null)
            {
                typeRef = null;
                Debug.LogError($"Unable to find nested type with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");
                return false;
            }

            return TryImport(moduleDef, typeDef, out typeRef);
        }

        public static bool TryFindMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var methods = type.GetMethods(bindingFlags);
            var found = (methodInfo = methods.FirstOrDefault(method => method.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                Debug.LogError($"Unable to find method info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        public static bool TryFindFieldWithAttribute<T> (System.Type type, out FieldInfo fieldInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var fields = type.GetFields(bindingFlags);
            var found = (fieldInfo = fields.FirstOrDefault(field => field.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                Debug.LogError($"Unable to find field info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        public static bool TryFindPropertyGetMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
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

        public static bool TryFindFieldDefinitionWithAttribute<T> (TypeDefinition typeDef, out FieldDefinition fieldDefinition) where T : Attribute
        {
            fieldDefinition = null;

            if (!TryImport(typeDef.Module, typeof(T), out var attributeType))
                return false;

            bool found = (fieldDefinition = typeDef.Fields
                .Where(field => field.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{typeDef.FullName}\".");

            return found;
        }

        public static bool TryFindParameterWithAttribute<T> (
            MethodDefinition methodDefinition, 
            out ParameterDefinition parameterDef)
        {
            if (!TryImport(methodDefinition.Module, typeof(T), out var parameterAttributeType))
            {
                parameterDef = null;
                return false;
            }

            bool found = (parameterDef = methodDefinition
                .Parameters
                .Where(parameter => parameter.CustomAttributes.Any(customAttributeData => customAttributeData.AttributeType.FullName == parameterAttributeType.FullName))
                .FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find parameter with attribute: \"{typeof(T).FullName}\" in method: \"{methodDefinition.Name}\" in type: \"{methodDefinition.DeclaringType.FullName}\".");

            return found;
        }

        public static bool TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<T> (CustomAttribute customAttribute, out int customAttributeArgumentIndex)
        {
            if (!TryImport(customAttribute.AttributeType.Module, typeof(T), out var customAttributeArgumentAttributeType))
            {
                customAttributeArgumentIndex = -1;
                return false;
            }

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

        public static bool ParameterIsString (ModuleDefinition moduleDefinition, ParameterDefinition parameterDef)
        {
            return 
                parameterDef.ParameterType.Namespace == moduleDefinition.TypeSystem.String.Namespace && 
                parameterDef.ParameterType.Name == moduleDefinition.TypeSystem.String.Name;
        }

        public static Instruction PushBUint (buint value)
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
                    if (value >= int.MaxValue)
                        throw new Exception($"Unable to add buffer unsigned integer push instruction, the value: {value} is larger then max value of int32: {int.MaxValue}");
                    return Instruction.Create(OpCodes.Ldc_I4, (int)value);
            }
        }

        public static Instruction PushInt (int value)
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

        public static void AddCustomAttributeToParameter<Attribute> (AssemblyDefinition compiledAssemblyDef, ParameterDefinition parameterDef)
        {
            if (!TryImport(compiledAssemblyDef.MainModule, typeof(Attribute), out var onTryCallMarkerAttributeTypeRef))
                return;

            var onTryCallMarkerAttributeTypeDef = onTryCallMarkerAttributeTypeRef.Resolve();
            var constructor = onTryCallMarkerAttributeTypeDef.Methods.FirstOrDefault(methodDef => methodDef.IsConstructor);
            parameterDef.CustomAttributes.Add(new CustomAttribute(Import(compiledAssemblyDef.MainModule, constructor)));
        }

        public static void AddCustomAttributeToMethod<Attribute> (ModuleDefinition moduleDef, MethodDefinition methoDef)
        {
            if (!TryImport(moduleDef, typeof(Attribute), out var attributeTypeRef))
                return;

            var attributeTypeDef = attributeTypeRef.Resolve();
            var constructor = attributeTypeDef.Methods.FirstOrDefault(method => method.IsConstructor);
            var customAttribute = new CustomAttribute(Import(moduleDef, constructor));
            methoDef.CustomAttributes.Add(customAttribute);
        }
    }
}
