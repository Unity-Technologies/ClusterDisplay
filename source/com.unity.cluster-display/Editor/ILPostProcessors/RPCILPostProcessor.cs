using System.Linq;
using UnityEngine;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using System;
using System.Reflection;
using Mono.Cecil;
using System.IO;
using Mono.Cecil.Cil;

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
                bool allMatch = methodDef.Name == rpcTokenizer.MethodName &&
                methodDef.ReturnType.Resolve().Module.Assembly.Name.Name == rpcTokenizer.DeclaringReturnTypeAssemblyName &&
                methodDef.HasParameters == rpcTokenizer.ParameterCount > 0 &&
                methodDef.Parameters.Count == rpcTokenizer.ParameterCount &&
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
                            parameterDefinition.ParameterType.Module.Assembly.Name.Name == rpcTokenizer[i].declaringParameterTypeAssemblyName;
                    }
                    return any;
                });

                return allMatch;

            }).FirstOrDefault()) != null;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!RPCSerializer.TryReadSerailizedRPCStubs(RPCRegistry.RPCStubsPath, out var serializedMethodStrings))
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

                var typeReference = assemblyDef.MainModule.ImportReference(typeDefinition);
                var methodReference = assemblyDef.MainModule.ImportReference(methodDefinition);

                var il = methodDefinition.Body.GetILProcessor();
                var firstInstruct = methodDefinition.Body.Instructions.First();

                var objectTypeReference = assemblyDef.MainModule.ImportReference(typeof(object)).Resolve();
                var stringTypeReference = assemblyDef.MainModule.ImportReference(typeof(string)).Resolve();
                var voidTypeReference = assemblyDef.MainModule.ImportReference(typeof(void)).Resolve();

                var debugTypeReference = assemblyDef.MainModule.ImportReference(typeof(Debug)).Resolve();
                var debugLogMethodReference = assemblyDef.MainModule.ImportReference(typeof(Debug).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(method => method.Name == "Log" && method.GetParameters().Length == 1).FirstOrDefault());

                var debugTypeDefinition = assemblyDef.MainModule.MetadataResolver.Resolve(debugTypeReference);

                try
                {
                    var nop = Instruction.Create(OpCodes.Nop);
                    il.InsertBefore(firstInstruct, nop);

                    var argInstruct = Instruction.Create(OpCodes.Ldstr, "Hello, World!");
                    il.InsertAfter(nop, argInstruct);

                    var logCallInstruct = Instruction.Create(OpCodes.Call, debugLogMethodReference);
                    il.InsertAfter(argInstruct, logCallInstruct);

                    il.InsertAfter(logCallInstruct, Instruction.Create(OpCodes.Nop));


                } catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                    return null;
                }

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
