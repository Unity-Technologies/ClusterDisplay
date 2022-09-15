using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using buint = System.UInt32;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    internal class CecilUtils
    {
        CodeGenDebug logger;
        public CecilUtils (CodeGenDebug logger) => this.logger = logger;

        public string ComputeMethodHash(MethodReference methodRef)
        {
            using (var sha1 = SHA1Managed.Create())
            {
                string methodSignature = MethodRefToSignature(methodRef);
                
                var methodSignatureBytes = System.Text.Encoding.ASCII.GetBytes(methodSignature);
                var hashBytes = sha1.ComputeHash(methodSignatureBytes);
                
                string hashStr = BitConverter.ToString(hashBytes);
                return hashStr.Replace("-", "");
            }
        }

        public string GenericTypeToSignature (GenericParameter typeDef)
        {
            string genericTypeSignature = TypeDefToSignature(typeDef.GenericParameters[0]);
            for (int i = 1; i < typeDef.GenericParameters.Count; i++)
                genericTypeSignature = $"{genericTypeSignature},{TypeDefToSignature(typeDef.GenericParameters[i])}";
            return $"{ParseGenericType(typeDef)}<{genericTypeSignature}>";
        }
        
        public string GenericTypeToSignature (TypeReference typeDef)
        {
            string genericTypeSignature = TypeDefToSignature(typeDef.GenericParameters[0]);
            for (int i = 1; i < typeDef.GenericParameters.Count; i++)
                genericTypeSignature = $"{genericTypeSignature},{TypeDefToSignature(typeDef.GenericParameters[i])}";
            return $"{ParseGenericType(typeDef)}<{genericTypeSignature}>";
        }
        
        public string ParseGenericType(GenericParameter typeDef) =>
            typeDef.FullName.Substring(0, typeDef.FullName.Length - 2);

        public string ParseGenericType(TypeReference typeDef) =>
            typeDef.FullName.Substring(0, typeDef.FullName.Length - 2);
        
        public string TypeDefToSignature(GenericParameter typeDef) =>
            $"{(typeDef.HasGenericParameters ? GenericTypeToSignature(typeDef) : typeDef.FullName)}";
        
        public string TypeDefToSignature(TypeReference typeDef) =>
            $"{(typeDef.HasGenericParameters ? GenericTypeToSignature(typeDef) : typeDef.FullName)}";

        public string MethodParametersToSignature(MethodDefinition methodDef)
        {
            if (methodDef.Parameters.Count == 0)
                return "";
            
            string parameterSignatures = TypeDefToSignature(methodDef.Parameters[0].ParameterType);
            for (int i = 1; i < methodDef.Parameters.Count; i++)
                parameterSignatures = $",{TypeDefToSignature(methodDef.Parameters[i].ParameterType)}";
            
            return parameterSignatures;
        }

        public string GenericMethodToSignature(MethodReference methodRef)
        {
            string genericTypeSignature = TypeDefToSignature(methodRef.GenericParameters[0]);
            for (int i = 1; i < methodRef.GenericParameters.Count; i++)
                genericTypeSignature = $"{genericTypeSignature},{TypeDefToSignature(methodRef.GenericParameters[i])}";
            return $"{methodRef.Name}<{genericTypeSignature}>";
        }

        public string MethodNameSignature(MethodReference methodRef) =>
            $"{TypeDefToSignature(methodRef.DeclaringType)}.{(methodRef.HasGenericParameters ? GenericMethodToSignature(methodRef) : methodRef.Name)}";

        public string MethodParametersSignature(MethodReference methodRef) =>
            $"{(methodRef.Parameters.Count > 0 ? $" {MethodParametersToSignature(methodRef.Resolve())}" : "")}";

        public string MethodRefToSignature(MethodReference methodRef) =>
            $"{TypeDefToSignature(methodRef.ReturnType)} {MethodNameSignature(methodRef)}{MethodParametersSignature(methodRef)}";
        
        public bool MethodDefinitionMatchesMethod(MethodDefinition methodDef, ConstructorInfo methodInfo)
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


        public bool MethodDefinitionMatchesMethod(MethodDefinition methodDef, MethodInfo methodInfo)
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

        public bool TryFindMatchingMethodDefinition (TypeDefinition typeDef, MethodInfo methodInfo, out MethodDefinition methodDef)
        {
            methodDef = typeDef.Methods.FirstOrDefault(methodDefinition => MethodDefinitionMatchesMethod(methodDefinition, methodInfo));

            if (methodDef == null)
            {
                logger.LogError($"Unable to find {nameof(MethodDefinition)} for method: \"{methodInfo.Name}\" in type: \"{typeDef.Namespace}.{typeDef.Name}\".");
                return false;
            }

            return true;
        }

        public bool TryFindMatchingMethodDefinition (TypeDefinition typeDef, ConstructorInfo constructorInfo, out MethodDefinition methodDef)
        {
            methodDef = typeDef.Methods.FirstOrDefault(methodDefinition => MethodDefinitionMatchesMethod(methodDefinition, constructorInfo));

            if (methodDef == null)
            {
                logger.LogError($"Unable to find {nameof(MethodDefinition)} for constructor: \"{constructorInfo.Name}\" in type: \"{typeDef.Namespace}.{typeDef.Name}\".");
                return false;
            }

            return true;
        }

        bool RecursivelyTryFindNestedType (TypeDefinition typeDef, Type type, out TypeDefinition nestedTypeDef)
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

        bool FindNestedType (ModuleDefinition moduleToSearch, Type type, out TypeDefinition nestedTypeDef)
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

        public bool TryImport (ModuleDefinition moduleToImportInto, Type type, out TypeReference importedTypeRef)
        {
            if (moduleToImportInto.Name != type.Module.Name)
            {
                importedTypeRef = moduleToImportInto.ImportReference(type);
                return importedTypeRef != null;
            }

            if (type.IsNested)
            {
                if (!FindNestedType(moduleToImportInto, type, out var nestedTypeDef))
                {
                    importedTypeRef = null;
                    return false;
                }

                importedTypeRef = nestedTypeDef;
                return (importedTypeRef = nestedTypeDef) != null;
            }

            else
            {
                importedTypeRef = moduleToImportInto.GetType(type.Namespace, type.Name);
                if (importedTypeRef != null)
                    return true;
            }

            logger.LogError($"Unable to find type definition to import for type: \"{type.Name}\".");
            importedTypeRef = null;
            return false;
        }

        public bool TryImport (ModuleDefinition moduleToImportInto, ConstructorInfo methodInfo, out MethodReference importedMethodRef)
        {
            if (moduleToImportInto.Name != methodInfo.Module.Name)
            {
                importedMethodRef = moduleToImportInto.ImportReference(methodInfo);
                return true;
            }

            if (!TryImport(moduleToImportInto, methodInfo.DeclaringType, out var typeDef))
            {
                logger.LogError($"Unable to import method: \"{methodInfo.Name}\" declared in: \"{methodInfo.DeclaringType.Name}\", cannot find declaring type reference.");
                importedMethodRef = null;
                return false;
            }

            if (!TryFindMatchingMethodDefinition(typeDef.Resolve(), methodInfo, out var matchingMethodDef))
            {
                logger.LogError($"Unable to find matching method definition for method: \"{methodInfo.Name}\" declared in: \"{methodInfo.DeclaringType.Name}\" to import.");
                importedMethodRef = null;
                return false;
            }

            importedMethodRef = matchingMethodDef;
            return (importedMethodRef = matchingMethodDef) != null;
        }

        public bool TryImport (ModuleDefinition moduleToImportInto, MethodInfo methodToImport, out MethodReference importedMethodRef)
        {
            if (methodToImport == null)
            {
                logger.LogError("NULL module.");
                importedMethodRef = null;
                return false;
            }
            
            if (moduleToImportInto == null)
            {
                logger.LogError($"Unable to import method: \"{methodToImport.Name}\" declared in: \"{methodToImport.DeclaringType.Name}\", the module were attempting to import into is null.");
                importedMethodRef = null;
                return false;
            }
            
            if (moduleToImportInto.Name != methodToImport.Module.Name)
            {
                importedMethodRef = moduleToImportInto.ImportReference(methodToImport);
                return true;
            }

            if (!TryImport(moduleToImportInto, methodToImport.DeclaringType, out var typeDef))
            {
                logger.LogError($"Unable to import method: \"{methodToImport.Name}\" declared in: \"{methodToImport.DeclaringType.Name}\", cannot find declaring type reference.");
                importedMethodRef = null;
                return false;
            }

            if (!TryFindMatchingMethodDefinition(typeDef.Resolve(), methodToImport, out var matchingMethodDef))
            {
                logger.LogError($"Unable to find matching method definition for method: \"{methodToImport.Name}\" declared in: \"{methodToImport.DeclaringType.Name}\" to import.");
                importedMethodRef = null;
                return false;
            }

            importedMethodRef = matchingMethodDef;
            return true;
        }

        public TypeReference Import (ModuleDefinition moduleToImportInto, TypeReference typeToImport)
        {
            if (moduleToImportInto.Name != typeToImport.Module.Name)
                return moduleToImportInto.ImportReference(typeToImport);
            return typeToImport;
        }

        public TypeReference Import (ModuleDefinition moduleToImportInto, TypeDefinition typeToImport)
        {
            if (moduleToImportInto.Name != typeToImport.Module.Name)
                return moduleToImportInto.ImportReference(typeToImport);
            return typeToImport;
        }

        public MethodReference Import (ModuleDefinition moduleToImportInto, MethodReference methodToImport)
        {
            if (moduleToImportInto.Name != methodToImport.Module.Name)
                return moduleToImportInto.ImportReference(methodToImport);
            return methodToImport;
        }

        public MethodReference Import (ModuleDefinition moduleToImportInto, MethodDefinition methodToImport)
        {
            if (moduleToImportInto.Name != methodToImport.Module.Name)
                return moduleToImportInto.ImportReference(methodToImport);
            return methodToImport;
        }

        public void InsertCallAfter (ILProcessor il, ref Instruction afterInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertCallBefore (ILProcessor il, Instruction beforeInstruction, MethodReference methodRef)
        {
            Instruction instruction = null;
            var methodDef = methodRef.Resolve();

            if (methodDef.IsVirtual || methodDef.IsAbstract)
                instruction = Instruction.Create(OpCodes.Callvirt, methodRef);
            else instruction = Instruction.Create(OpCodes.Call, methodRef);

            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void IsertPushLocalVariableAfter (ILProcessor il, ref Instruction afterInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction IsertPushLocalVariableBefore (ILProcessor il, Instruction beforeInstruction, VariableDefinition variableDef)
        {
            var instruction = Instruction.Create(OpCodes.Ldloca, variableDef);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertPushParameterToStackAfter (ILProcessor il, ref Instruction afterInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertPushParameterToStackBefore (ILProcessor il, Instruction beforeInstruction, ParameterDefinition parameterDef, bool isStaticCaller, bool byReference)
        {
            var instruction = PushParameterToStack(parameterDef, isStaticCaller, byReference);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertPushBufferUIntAfter (ILProcessor il, ref Instruction afterInstruction, buint integer)
        {
            var instruction = PushBUint(integer);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertPushBufferUIntBefore (ILProcessor il, Instruction beforeInstruction, buint integer)
        {
            var instruction = PushBUint(integer);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertPushIntAfter (ILProcessor il, ref Instruction afterInstruction, int integer)
        {
            var instruction = PushInt((int)integer);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertPushIntBefore (ILProcessor il, Instruction beforeInstruction, int integer)
        {
            var instruction = PushInt((int)integer);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertPushStringAfter (ILProcessor il, ref Instruction afterInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertPushStringBefore (ILProcessor il, Instruction beforeInstruction, string str)
        {
            var instruction = Instruction.Create(OpCodes.Ldstr, str);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertPushThisAfter (ILProcessor il, ref Instruction afterInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertPushThisBefore (ILProcessor il, Instruction beforeInstruction)
        {
            var instruction = Instruction.Create(OpCodes.Ldarg_0);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, int operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode, operand);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode, Instruction operand)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public void InsertAfter (ILProcessor il, ref Instruction afterInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertAfter(afterInstruction, instruction);
            afterInstruction = instruction;
        }

        public Instruction InsertBefore (ILProcessor il, Instruction beforeInstruction, OpCode opCode)
        {
            var instruction = Instruction.Create(opCode);
            il.InsertBefore(beforeInstruction, instruction);
            return instruction;
        }

        public bool MethodIsCoroutine (MethodDefinition methodDef)
        {
            if (!TryImport(methodDef.Module, typeof(System.Collections.IEnumerator), out var typeRef))
                return false;

            return methodDef.ReturnType.MetadataToken == typeRef.MetadataToken;
        }

        public bool TryPushMethodRef<DelegateMarker> (AssemblyDefinition compiledAssemblyDef, MethodReference methodRef, ILProcessor constructorILProcessor)
            where DelegateMarker : Attribute
        {
            constructorILProcessor.Emit(OpCodes.Ldnull);
            constructorILProcessor.Emit(OpCodes.Ldftn, methodRef);

            if (!TryFindNestedTypeWithAttribute<DelegateMarker>(compiledAssemblyDef.MainModule, typeof(RPCInterfaceRegistry), out var delegateTypeRef))
                return false;

            var delegateTypeDef = delegateTypeRef.Resolve();
            if (delegateTypeDef == null)
                return false;

            var constructorMethodDef = delegateTypeDef.Methods.FirstOrDefault(method => method != null && method.IsConstructor);
            var constructorMethodRef = Import(compiledAssemblyDef.MainModule, constructorMethodDef);
            if (constructorMethodRef == null)
                return false;

            constructorILProcessor.Emit(OpCodes.Newobj, constructorMethodRef);
            return true;
        }

        public bool TryDetermineSizeOfPrimitive (TypeDefinition typeDef, ref buint size)
        {
            switch (typeDef.Name)
            {
                case "Byte":
                    size += (buint)Marshal.SizeOf<Byte>();
                    return true;
                
                case "SByte":
                    size += (buint)Marshal.SizeOf<SByte>();
                    return true;
                
                case "Int16":
                    size += (buint)Marshal.SizeOf<Int16>();
                    return true;
                
                case "UInt16":
                    size += (buint)Marshal.SizeOf<UInt16>();
                    return true;
                
                // In C#, Booleans are 1 byte. However, Marshal.SizeOf<bool> will return 4 bytes as backwards
                // compatibility for Windows SDK's BOOL which is a typedef for int. The user would need to use:
                // [MarshalAs(UnmanagedType.I1)] to explicitly flag to Marshal that it should convert the 
                // bool into a single byte. For now this is the cleaner solution.
                // https://stackoverflow.com/a/39251864
                case "Boolean":
                    size += (buint)Marshal.SizeOf<Boolean>();
                    return true;
                    
                // We want to use C#'s 2 byte char to respect unicode instead of Marshal casting char to 1 byte.
                case "Char":
                    size += sizeof(char);
                    return true;
                    
                case "Int32":
                    size += (buint)Marshal.SizeOf<Int32>();
                    return true;
                
                case "UInt32":
                    size += (buint)Marshal.SizeOf<UInt32>();
                    return true;
                
                case "Single":
                    size += (buint)Marshal.SizeOf<Single>();
                    return true;
                
                case "Int64":
                    size += (buint)Marshal.SizeOf<Int64>();
                    return true;
                
                case "UInt64":
                    size += (buint)Marshal.SizeOf<UInt64>();
                    return true;
                
                case "Double":
                    size += (buint)Marshal.SizeOf<Double>();
                    return true;
                
                default:
                    logger.LogError($"Unable to determine size of assumed primitive type: \"{typeDef.Name}\".");
                    return false;
            }
        }

        public bool TryDetermineSizeOfStruct (TypeDefinition typeDefinition, ref buint size)
        {
            if (!typeDefinition.IsAutoLayout && typeDefinition.ClassSize != -1) // Is this struct using [StructLayout] with an explicit byte size.
            {
                size += (buint)typeDefinition.ClassSize;
                return true;
            }

            bool allValid = true;

            foreach (var field in typeDefinition.Fields)
            {
                if (field.IsStatic)
                    continue;
                allValid &= TryDetermineSizeOfField(field, ref size);
            }

            return allValid;
        }

        public bool TryDetermineSizeOfField(FieldDefinition fieldDef, ref buint size)
        {
            if (fieldDef.HasMarshalInfo)
            {
                switch (fieldDef.MarshalInfo.NativeType)
                {
                    case NativeType.I1:
                    case NativeType.U1:
                        size += 1;
                        return true;
                    
                    case NativeType.I2:
                    case NativeType.U2:
                        size += 2;
                        return true;
                    
                    case NativeType.Boolean:
                    case NativeType.I4:
                    case NativeType.U4:
                        size += 4;
                        return true;
                    
                    case NativeType.I8:
                    case NativeType.U8:
                        size += 8;
                        return true;
                    
                    default:
                        logger.LogError($"Unsupported unmanaged type: \"{fieldDef.MarshalInfo.NativeType}\" declared for primitive type: \"{fieldDef.FieldType.Name}\".");
                        return false;
                }
            }
            
            return TryDetermineSizeOfValueType(fieldDef.FieldType, ref size);
        }

        public bool TryDetermineSizeOfValueType (TypeReference typeRef, ref buint size)
        {
            var typeDef = typeRef.Resolve();
            if (typeDef == null)
                throw new Exception($"Unable to determine size of type: \"{typeRef.Name}\", the resolved type definition is NULL!");
            
            if (typeDef.IsPrimitive)
                return TryDetermineSizeOfPrimitive(typeDef, ref size);

            else if (typeDef.IsEnum)
            {
                var fieldDef = typeDef.Fields.FirstOrDefault(field => field.Name == "value__");
                return TryDetermineSizeOfPrimitive(fieldDef.FieldType.Resolve(), ref size);
            }

            else if (typeRef.IsValueType)
                return TryDetermineSizeOfStruct(typeDef, ref size);

            throw new Exception($"Unable to determine size of supposed value type: \"{typeRef.FullName}\".");
        }

        public Instruction PushParameterToStack (ParameterDefinition parameterDefinition, bool isStaticCaller, bool byReference)
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

        public void ForeachMethodDef (TypeDefinition declaringTypeDef, Func<MethodDefinition, bool> callback)
        {
            Parallel.ForEach(declaringTypeDef.Methods, (methodDef, loopState) =>
            {
                if (!callback(methodDef))
                    loopState.Break();
            });
        }

        public void ForeachNestedType (TypeDefinition containerTypeDef, Func<TypeDefinition, bool> callback)
        {
            Parallel.ForEach(containerTypeDef.NestedTypes, (typeDef, loopState) =>
            {
                if (!callback(typeDef))
                    loopState.Break();
            });
        }

        public void ForeachTypeDef (ModuleDefinition moduleDef, Func<TypeDefinition, bool> callback)
        {
            Parallel.ForEach(moduleDef.Types, (typeDef, loopState) =>
            {
                if (!callback(typeDef))
                    loopState.Break();
            });
        }

        public bool TryFindTypeDefByName (ModuleDefinition moduleDef, string typeName, out TypeDefinition matchingTypeDef)
        {
            TypeDefinition foundTypeDef = null;
            ForeachTypeDef(moduleDef, (typeDef) =>
            {
                if (typeDef.Name != typeName)
                    return true;

                foundTypeDef = typeDef;
                return false;
            });

            return (matchingTypeDef = foundTypeDef) != null;
        }

        public bool TryFindTypeDefByNamespaceAndName (ModuleDefinition moduleDef, string namespaceStr, string typeName, out TypeDefinition matchingTypeDef)
        {
            TypeDefinition foundTypeDef = null;
            ForeachTypeDef(moduleDef, (typeDef) =>
            {
                if (string.IsNullOrEmpty(typeDef.Namespace) != string.IsNullOrEmpty(namespaceStr))
                    return true;
                
                if (typeDef.Namespace != namespaceStr || typeDef.Name != typeName)
                    return true;

                foundTypeDef = typeDef;
                return false;
            });

            return (matchingTypeDef = foundTypeDef) != null;
        }

        public bool TryGetTypeDefByName (ModuleDefinition moduleDef, string namespaceStr, string typeName, out TypeDefinition matchingTypeDef)
        {
            if (typeName == typeof(void).Name)
            {
                matchingTypeDef = moduleDef.TypeSystem.Void.Resolve();
                return true;
            }

            if (RPCSerializer.TryParseNestedAddressIfAvailable(namespaceStr, out var rootTypeNamespace, out var nestedTypeNames))
            {
                TypeDefinition[] nestedTypes = new TypeDefinition[nestedTypeNames.Length];
                if (!TryFindTypeDefByName(moduleDef, nestedTypeNames[0], out var rootContainerTypeDef))
                {
                    matchingTypeDef = null;
                    return false;
                }

                nestedTypes[0] = rootContainerTypeDef;

                for (int nti = 1; nti < nestedTypeNames.Length; nti++)
                {
                    ForeachNestedType(rootContainerTypeDef, (nestedType) =>
                    {
                        if (nestedType.Name != nestedTypeNames[nti])
                            return true;

                        nestedTypes[nti] = nestedType;
                        return false;
                    });
                }

                return (matchingTypeDef = nestedTypes[nestedTypes.Length - 1]) != null;
            }

            if (!string.IsNullOrEmpty(namespaceStr))
                return TryFindTypeDefByNamespaceAndName(moduleDef, namespaceStr, typeName, out matchingTypeDef);
            return TryFindTypeDefByName(moduleDef, typeName, out matchingTypeDef);
        }
        public bool TryFindMatchingMethodInTypeDef (
            ModuleDefinition moduleDef,
            TypeDefinition typeDef, 
            ref RPCStub rpcStub, 
            out MethodDefinition outMethodDef)
        {
            MethodDefinition methodDef = null;
            outMethodDef = null;

            var rpc = rpcStub;

            ForeachMethodDef(typeDef, (method) =>
            {
                if (method.Name != rpc.methodStub.methodName || method.ReturnType.Name != rpc.methodStub.returnTypeName)
                    return true;

                if (rpc.methodStub.ParameterCount > 0)
                {
                    if (method.Parameters.Count == 0 || method.Parameters.Count != rpc.methodStub.ParameterCount)
                        return true;

                    bool allParametersMatch = true;
                    for (int pi = 0; pi < method.Parameters.Count; pi++)
                    {
                        if (!(allParametersMatch &= method.Parameters[pi].Name == rpc.methodStub[pi].parameterName))
                            return true;

                        allParametersMatch &=
                            method.Parameters[pi].ParameterType.Name == rpc.methodStub.parameterTypeName[pi] &&
                            method.Parameters[pi].Name == rpc.methodStub.parameterNames[pi];
                    }

                    if (allParametersMatch)
                        goto found;
                }

                else if (!method.HasParameters)
                    goto found;

                return true;

                found:
                methodDef = method;
                return false;

            });

            return (outMethodDef = methodDef) != null;
        }

        public bool TryGetMethodReference (
            ModuleDefinition moduleDef,
            TypeDefinition typeDef, 
            ref RPCStub rpcStub, 
            out MethodReference methodRef)
        {
            if (!TryFindMatchingMethodInTypeDef(moduleDef, typeDef, ref rpcStub, out var methodDef))
            {
                logger.LogError($"Unable to find method reference for serialized RPC: \"{rpcStub.methodStub.methodName}\" declared in: \"{typeDef.FullName}\".");
                methodRef = null;
                return false;
            }

            methodRef = Import(moduleDef, methodDef);
            return true;
        }

        public bool TryFindNestedTypeWithAttribute<T> (ModuleDefinition moduleDef, Type type, out TypeReference typeRef, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var typeDef = nestedTypes.FirstOrDefault(nestedType => nestedType.GetCustomAttribute<T>() != null);

            if (typeDef == null)
            {
                typeRef = null;
                logger.LogError($"Unable to find nested type with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");
                return false;
            }

            return TryImport(moduleDef, typeDef, out typeRef);
        }

        public bool TryFindMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var methods = type.GetMethods(bindingFlags);
            var found = (methodInfo = methods.FirstOrDefault(method => method.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                logger.LogError($"Unable to find method info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        public bool TryFindFieldWithAttribute<T> (System.Type type, out FieldInfo fieldInfo, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) where T : Attribute
        {
            var attributeType = typeof(T);
            var fields = type.GetFields(bindingFlags);
            var found = (fieldInfo = fields.FirstOrDefault(field => field.GetCustomAttribute<T>() != null)) != null;

            if (!found)
                logger.LogError($"Unable to find field info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return found;
        }

        public bool TryFindPropertyGetMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
        {
            methodInfo = null;
            var attributeType = typeof(T);
            var propertyInfo = type.GetProperties()
                .Where(pi => pi.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault();

            if (propertyInfo == null || (methodInfo = propertyInfo.GetGetMethod()) == null)
                logger.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

            return methodInfo != null;
        }

        public bool TryFindFieldDefinitionWithAttribute<T> (TypeDefinition typeDef, out FieldDefinition fieldDefinition) where T : Attribute
        {
            fieldDefinition = null;

            if (!TryImport(typeDef.Module, typeof(T), out var attributeType))
                return false;

            bool found = (fieldDefinition = typeDef.Fields
                .Where(field => field.CustomAttributes.Any(customAttribute => customAttribute.AttributeType == attributeType))
                .FirstOrDefault()) != null;

            if (!found)
                logger.LogError($"Unable to find property getter with attribute: \"{typeof(T).FullName}\" in type: \"{typeDef.FullName}\".");

            return found;
        }

        public bool TryFindParameterWithAttribute<T> (
            MethodDefinition methodDefinition, 
            out ParameterDefinition parameterDef)
        {
            parameterDef = null;
            if (!TryImport(methodDefinition.Module, typeof(T), out var parameterAttributeType))
                return false;

            for (int pi = 0; pi < methodDefinition.Parameters.Count; pi++)
            {
                if (!methodDefinition.Parameters[pi].CustomAttributes.Any(customAttributeData => customAttributeData.AttributeType.FullName == parameterAttributeType.FullName))
                    continue;

                parameterDef = methodDefinition.Parameters[pi];
                break;
            }

            bool found = parameterDef != null;

            if (!found)
                logger.LogError($"Unable to find parameter with attribute: \"{typeof(T).FullName}\" in method: \"{methodDefinition.Name}\" in type: \"{methodDefinition.DeclaringType.FullName}\".");

            return found;
        }

        public bool TryFindIndexOfCustomAttributeConstructorArgumentWithAttribute<T> (CustomAttribute customAttribute, out int customAttributeArgumentIndex)
        {
            if (!TryImport(customAttribute.AttributeType.Module, typeof(T), out var customAttributeArgumentAttributeType))
            {
                customAttributeArgumentIndex = -1;
                return false;
            }

            var constructorMethodDef = customAttribute.Constructor.Resolve();

            for (int i = 0; i < constructorMethodDef.Parameters.Count; i++)
            {
                var parameterDef = constructorMethodDef.Parameters[i].Resolve();
                if (!parameterDef.CustomAttributes.Any(parameterCustomAttribute => parameterCustomAttribute.AttributeType.FullName == customAttributeArgumentAttributeType.FullName))
                    continue;

                customAttributeArgumentIndex = i;
                return true;
            }

            logger.LogError($"Unable to find index of custom attribute constructor argument with attribute: \"{typeof(T).FullName}\".");
            customAttributeArgumentIndex = -1;
            return false;
        }

        public bool ParameterIsString (ModuleDefinition moduleDefinition, ParameterDefinition parameterDef)
        {
            return 
                parameterDef.ParameterType.Namespace == moduleDefinition.TypeSystem.String.Namespace && 
                parameterDef.ParameterType.Name == moduleDefinition.TypeSystem.String.Name;
        }

        public Instruction PushBUint (buint value)
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

        public Instruction PushInt (int value)
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

        public void AddCustomAttributeToParameter<Attribute> (AssemblyDefinition compiledAssemblyDef, ParameterDefinition parameterDef)
        {
            var constructorMethodInfo = typeof(Attribute).GetConstructors()[0];
            if (!TryImport(compiledAssemblyDef.MainModule, constructorMethodInfo, out var constructorMethodRef))
                return;
            parameterDef.CustomAttributes.Add(new CustomAttribute(constructorMethodRef));
        }

        public CustomAttribute AddCustomAttributeToMethod<Attribute> (ModuleDefinition moduleDef, MethodDefinition methoDef)
        {
            var constructorMethodInfo = typeof(Attribute).GetConstructors()[0];
            if (!TryImport(moduleDef, constructorMethodInfo, out var constructorMethodRef))
                return null;

            var customAttribute = new CustomAttribute(constructorMethodRef);
            methoDef.CustomAttributes.Add(customAttribute);
            return customAttribute;
        }
    }
}
