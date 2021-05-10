using System.Linq;
using UnityEngine;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using System;
using System.Reflection;
using Mono.Cecil;
using System.IO;
using Mono.Cecil.Cil;
using System.Runtime.InteropServices;

namespace Unity.ClusterDisplay
{
    public class RPCILPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance()
        {
            return this;
        }

        private const string attributeSearchAssemblyName = "ILPostprocessorAttributes";

        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }
        private static string GetAssemblyLocation (AssemblyNameReference name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name.Name).Location;
        private static string GetAssemblyLocation (string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == name).Location;

        private class AssemblyResolver : BaseAssemblyResolver
        {
            private DefaultAssemblyResolver _defaultResolver;
            public AssemblyResolver()
            {
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                string assemblyLocation = GetAssemblyLocation(name);
                try
                {

                    parameters.AssemblyResolver = this;
                    parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                    return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assemblyLocation)), parameters);

                } catch (AssemblyResolutionException ex)
                {
                    Debug.LogException(ex);
                    return null;
                }
            }
        }

        private bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
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

        private bool TryGetMethodDefinition (TypeDefinition typeDefinition, ref RPCSerializer.RPCTokenizer inRPCTokenizer, out MethodDefinition methodDefinition)
        {
            var rpcTokenizer = inRPCTokenizer;
            return (methodDefinition = typeDefinition.Methods.Where(methodDef =>
            {
                // Debug.Log($"Method Signature: {methodDef.Name} == {rpcTokenizer.MethodName} &&\n{methodDef.ReturnType.Resolve().Module.Assembly.Name.Name} == {rpcTokenizer.DeclaringReturnTypeAssemblyName} &&\n{methodDef.HasParameters} == {rpcTokenizer.ParameterCount > 0} &&\n{methodDef.Parameters.Count} == {rpcTokenizer.ParameterCount}");
                bool allMatch = 
                    methodDef.HasParameters == rpcTokenizer.ParameterCount > 0 &&
                    methodDef.Parameters.Count == rpcTokenizer.ParameterCount &&
                    methodDef.Name == rpcTokenizer.MethodName &&
                    methodDef.ReturnType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.DeclaringReturnTypeAssemblyName &&
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

        private bool TryGetOpCodeForParameter (int parameterIndex, out OpCode opCode)
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

        private void InjectOpenRPCLatchCall (
            ILProcessor il, 
            Instruction beforeInstruction, 
            bool isStatic, 
            ushort rpcId, 
            MethodReference call, 
            /*int sizeOfAllParameters,*/
            out Instruction previousInstruction)
        {
            Instruction newInstruct = null;
            if (isStatic)
            {
                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertBefore(beforeInstruction, newInstruct);
                previousInstruction = newInstruct;
            }

            else
            {
                newInstruct = Instruction.Create(OpCodes.Ldarg_0);
                il.InsertBefore(beforeInstruction, newInstruct);
                previousInstruction = newInstruct;

                newInstruct = Instruction.Create(OpCodes.Ldc_I4, rpcId);
                il.InsertAfter(previousInstruction, newInstruct);
                previousInstruction = newInstruct;
            }

            /*
            newInstruct = Instruction.Create(OpCodes.Ldc_I4, sizeOfAllParameters);
            il.InsertAfter(previousInstruction, newInstruct);
            previousInstruction = newInstruct;
            */

            newInstruct = Instruction.Create(OpCodes.Call, call);
            il.InsertAfter(previousInstruction, newInstruct);
            previousInstruction = newInstruct;
        }

        private bool TryDetermineSizeOfPrimitive (string typeName, ref int size)
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
                    return false;
            }
        }

        private bool TryDetermineSizeOfStruct (TypeDefinition typeDefinition, ref int size)
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

        private bool TryDetermineSizeOfType (TypeDefinition typeDefinition, ref int size)
        {
            if (typeDefinition.IsPrimitive || typeDefinition.IsEnum)
                return TryDetermineSizeOfPrimitive(typeDefinition.Name, ref size);
            else if (typeDefinition.IsValueType)
                return TryDetermineSizeOfStruct(typeDefinition, ref size);
            return false;
        }

        private bool TryBridge (
            AssemblyDefinition assemblyDef, 
            ILProcessor il, 
            Instruction beforeInstruction, 
            ushort rpcId, 
            MethodDefinition targetMethod)
        {
            var rpcEmitterType = typeof(RPCEmitter);

            var rpcEmitterTypeReference = assemblyDef.MainModule.ImportReference(rpcEmitterType);

            var openRPCLatchMethodRef = assemblyDef.MainModule.ImportReference(rpcEmitterType.GetMethod(
                targetMethod.IsStatic ? 
                "AppendStaticRPCCall" : 
                "AppendRPCCall"));

            var copyValueToBufferMethodRef = assemblyDef.MainModule.ImportReference(rpcEmitterType.GetMethod("CopyValueToBuffer"));

            var parameters = targetMethod.Parameters;
            /*
            int sizeOfAllParameters = 0;
            foreach (var param in parameters)
            {
                var typeReference = assemblyDef.MainModule.ImportReference(param.ParameterType);

                int sizeOfType = 0;
                if (!TryDetermineSizeOfType(typeReference.Resolve(), ref sizeOfType))
                    return false;

                sizeOfAllParameters += sizeOfType;
            }
            */

            Instruction newInstruction = null;
            Instruction previousInstruction = null;

            InjectOpenRPCLatchCall(
                il, 
                beforeInstruction, 
                targetMethod.IsStatic, 
                rpcId, 
                openRPCLatchMethodRef, 
                /*sizeOfAllParameters,*/
                out previousInstruction);

            int parameterIndex = 0;
            foreach (var param in parameters)
            {
                var genericInstanceMethod = new GenericInstanceMethod(copyValueToBufferMethodRef);
                genericInstanceMethod.GenericArguments.Add(param.ParameterType);

                if (!TryGetOpCodeForParameter(parameterIndex++, out var opCode))
                    return false;

                newInstruction = Instruction.Create(opCode);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;

                newInstruction = Instruction.Create(OpCodes.Call, genericInstanceMethod);
                il.InsertAfter(previousInstruction, newInstruction);
                previousInstruction = newInstruction;
            }

            /*
            var lastInstruction = il.Body.Instructions[il.Body.Instructions.Count - 1];
            il.InsertBefore(lastInstruction, Instruction.Create(OpCodes.Nop));
            */

            return true;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out var serializedMethodStrings))
                return null;

            if (compiledAssembly.Name != ReflectionUtils.DefaultUserAssemblyName)
                return null;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out var assemblyDef))
                return null;

            foreach (var methodStr in serializedMethodStrings)
            {
                var rpcTokenizer = new RPCSerializer.RPCTokenizer(methodStr);
                if (!rpcTokenizer.IsValid)
                    continue;

                if (rpcTokenizer.DeclaringAssemblyName != compiledAssembly.Name)
                    continue;

                Debug.Log($"Post processing compiled assembly: \"{compiledAssembly.Name}\".");

                var typeDefinition = assemblyDef.MainModule.GetType(rpcTokenizer.DeclaringTypeFullName);
                if (!TryGetMethodDefinition(typeDefinition, ref rpcTokenizer, out var methodDefinition))
                {
                    Debug.LogError($"Unable to find method signature: \"{methodStr}\".");
                    continue;
                }

                var il = methodDefinition.Body.GetILProcessor();
                var firstInstruct = methodDefinition.Body.Instructions.First();

                if (!TryBridge(assemblyDef, il, firstInstruct, rpcTokenizer.RPCId, methodDefinition))
                    continue;

                Debug.Log($"Injected RPC intercept assembly into method: \"{methodDefinition.Name}\" in class: \"{methodDefinition.DeclaringType.FullName}\".");
            }

            var pe = new MemoryStream();
            var pdb = new MemoryStream();
            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(), SymbolStream = pdb, WriteSymbols = true
            };

            assemblyDef.Write(pe, writerParameters);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return true;
        }
    }
}
