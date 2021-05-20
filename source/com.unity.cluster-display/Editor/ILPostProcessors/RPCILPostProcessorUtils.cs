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
                allValid &= TryDetermineSizeOfType(field.FieldType.Resolve(), ref size);
            }

            return allValid;
        }

        private static bool TryDetermineSizeOfType (TypeDefinition typeDefinition, ref int size)
        {
            if (typeDefinition.IsPrimitive || typeDefinition.IsEnum)
                return TryDetermineSizeOfPrimitive(typeDefinition.Name, ref size);
            else if (typeDefinition.IsValueType)
                return TryDetermineSizeOfStruct(typeDefinition, ref size);
            return false;
        }

        private static bool TryGetOpCodeForParameter (int parameterIndex, out OpCode opCode)
        {
            opCode = OpCodes.Nop;
            switch (parameterIndex)
            {
                case 0:
                    opCode = OpCodes.Ldarg_1;
                    return true;
                case 1:
                    opCode = OpCodes.Ldarg_2;
                    return true;
                case 2:
                    opCode = OpCodes.Ldarg_3;
                    return true;
            }

            return false;
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

        private static bool TryFindMethodDefinitionWithAttribute (TypeDefinition typeDef, TypeReference attributeTypeRef, out MethodDefinition methodDef)
        {
            var found = (methodDef = typeDef.Methods
                .Where(method => method.CustomAttributes
                    .Any(customAttribute =>
                    {
                        return
                            customAttribute.AttributeType.FullName == attributeTypeRef.FullName;
                    })).FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find method definition with attribute: \"{attributeTypeRef.FullName}\" in type: \"{typeDef.FullName}\".");

            return found;
        }

        private static bool TryFindMethodWithAttribute<T> (System.Type type, out MethodInfo methodInfo) where T : Attribute
        {
            var attributeType = typeof(T);
            var found = (methodInfo = type.GetMethods()
                .Where(method => method.CustomAttributes
                    .Any(customAttribute =>
                    {
                        return
                            customAttribute.AttributeType.FullName == attributeType.FullName;
                    })).FirstOrDefault()) != null;

            if (!found)
                Debug.LogError($"Unable to find method info with attribute: \"{typeof(T).FullName}\" in type: \"{type.FullName}\".");

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

        private static bool TryFindParameterWithAttribute<T> (MethodDefinition methodDefinition, out ParameterDefinition parameterDef)
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

        private static bool TryGetMethodDefinition (TypeDefinition typeDefinition, ref SerializedRPC inRPCTokenizer, out MethodDefinition methodDefinition)
        {
            var rpcTokenizer = inRPCTokenizer;
            return (methodDefinition = typeDefinition.Methods.Where(methodDef =>
            {
                // Debug.Log($"Method Signature: {methodDef.Name} == {rpcTokenizer.MethodName} &&\n{methodDef.ReturnType.Resolve().Module.Assembly.Name.Name} == {rpcTokenizer.DeclaringReturnTypeAssemblyName} &&\n{methodDef.HasParameters} == {rpcTokenizer.ParameterCount > 0} &&\n{methodDef.Parameters.Count} == {rpcTokenizer.ParameterCount}");
                bool allMatch = 
                    methodDef.HasParameters == rpcTokenizer.ParameterCount > 0 &&
                    methodDef.Parameters.Count == rpcTokenizer.ParameterCount &&
                    methodDef.Name == rpcTokenizer.methodName &&
                    methodDef.ReturnType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.declaringReturnTypeAssemblyName &&
                    methodDef.Parameters.All(parameterDefinition =>
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
        }

        private static void InjectOpenRPCLatchCall (
            ILProcessor il, 
            Instruction afterInstruction, 
            bool isStatic, 
            ushort rpcId, 
            MethodReference call, 
            ushort sizeOfAllParameters,
            out Instruction lastInstruction)
        {
            Instruction newInstruct = null;
            if (isStatic)
            {
                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertAfter(afterInstruction, newInstruct);
                afterInstruction = newInstruct;
            }

            else
            {
                newInstruct = Instruction.Create(OpCodes.Ldarg_0);
                il.InsertAfter(afterInstruction, newInstruct);
                afterInstruction = newInstruct;

                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertAfter(afterInstruction, newInstruct);
                afterInstruction = newInstruct;

                /*
                newInstruct = Instruction.Create(OpCodes.Conv_U2);
                il.InsertAfter(afterInstruction, newInstruct);
                lastInstruction = newInstruct;
                */
            }

            newInstruct = Instruction.Create(OpCodes.Ldc_I4, sizeOfAllParameters);
            il.InsertAfter(afterInstruction, newInstruct);
            afterInstruction = newInstruct;

            /*
            newInstruct = Instruction.Create(OpCodes.Conv_U2);
            il.InsertAfter(afterInstruction, newInstruct);
            afterInstruction = newInstruct;
            */

            newInstruct = Instruction.Create(OpCodes.Call, call);
            il.InsertAfter(afterInstruction, newInstruct);
            lastInstruction = newInstruct;
        }
    }
}
