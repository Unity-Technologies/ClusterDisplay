#define CLUSTER_DISPLAY_ILPOSTPROCESSING

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    
    /// <summary>
    /// This class asynchronously loads in recently compiled assemblies 
    /// and determines whether we should manipulate them.
    /// </summary>
    public class AssemblyILPostProcessor : ILPostProcessor
    {
        /// <summary>
        /// This class builds a path to the assembly location, loads the DLL bytes and
        /// converts those bytes into a Cecil AssemblyDefinition which we manipulate.
        /// </summary>
        internal sealed class AssemblyResolver : BaseAssemblyResolver
        {
            private DefaultAssemblyResolver _defaultResolver;
            public AssemblyResolver()
            {
                _defaultResolver = new DefaultAssemblyResolver();
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (!ReflectionUtils.TryGetAssemblyLocation(name.Name, out var assemblyLocation))
                {
                    CodeGenDebug.LogError($"Unable to resolve assembly: \"{name.Name}\", unknown location.");
                    return null;
                }
                
                try
                {

                    parameters.AssemblyResolver = this;
                    parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                    return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assemblyLocation)), parameters);

                } catch (AssemblyResolutionException ex)
                {
                    CodeGenDebug.LogException(ex);
                    return null;
                }
            }
        }

        internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
        {
            public IReflectionImporter GetReflectionImporter(ModuleDefinition module) => new PostProcessorReflectionImporter(module);
        }

        internal class PostProcessorReflectionImporter : DefaultReflectionImporter
        {
            private const string SystemPrivateCoreLib = "System.Private.CoreLib";
            private AssemblyNameReference _correctCorlib;

            public PostProcessorReflectionImporter(ModuleDefinition module) : base(module) =>
                _correctCorlib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == SystemPrivateCoreLib);

            public override AssemblyNameReference ImportReference(System.Reflection.AssemblyName reference) =>
                _correctCorlib != null && reference.Name == SystemPrivateCoreLib ? 
                    _correctCorlib : 
                    base.ImportReference(reference);
        }

        private string[] cachedRegisteredAssemblyFullNames = null;

        /// <summary>
        /// Loads the assembly into memory and builds a Cecil AssemblyDefinition.
        /// </summary>
        /// <param name="compiledAssembly"></param>
        /// <param name="assemblyDef"></param>
        /// <returns></returns>
        public static bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
        {
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(),
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate,
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            try
            {
                assemblyDef = AssemblyDefinition.ReadAssembly(peStream, readerParameters);
                return true;
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                assemblyDef = null;
                return false;
            }
        }

        /// <summary>
        /// Load in the assembly debugging information if it exists.
        /// </summary>
        /// <param name="assemblyLocation"></param>
        /// <returns></returns>
        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }

        public override ILPostProcessor GetInstance() => this;
        /// <summary>
        /// This is where ILPostProcessing starts for a specific assembly. NOTE: This may be executed multiple times asynchronously per assembly.
        /// </summary>
        /// <param name="compiledAssembly">The assembly that was just compiled.</param>
        /// <returns></returns>
        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            CodeGenDebug.BeginILPostProcessing();

            /*
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var msg = $"Assemblies:\n\t{assemblies[0].GetName().Name},";
            for (int i = 1; i < assemblies.Length; i++)
                msg = $"{msg}\n\t{assemblies[i].GetName().Name}";
            CodeGenDebug.Log(msg);
            */

            CodeGenDebug.Log($"Post processing assembly: \"{compiledAssembly.Name}\".");
            
            // Determine which assemblies we are allowed to manipulate.
            if (cachedRegisteredAssemblyFullNames == null)
                RPCSerializer.TryReadRegisteredAssembliesFile(RPCRegistry.k_RegisteredAssembliesJsonPath, out cachedRegisteredAssemblyFullNames);
            
            // Determine whether the assembly we are about to process is allowed to be manipulated.
            AssemblyDefinition compiledAssemblyDef = null;
            if (!cachedRegisteredAssemblyFullNames.Any(registeredAssembly => registeredAssembly == compiledAssembly.Name))
                goto ignoreAssembly;

            // Get the Cecil AssemblyDefinition for the assembly.
            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out compiledAssemblyDef))
                goto failure;

            // Now we manipulate the assembly's IL.
            RPCILPostProcessor postProcessor = new RPCILPostProcessor();
            if (!postProcessor.TryProcess(compiledAssemblyDef))
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
                CodeGenDebug.LogException(exception);
                goto failure;
            }

            CodeGenDebug.Log($"Post processed assembly: {compiledAssembly.Name}");
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));

            ignoreAssembly:
            return null;

            failure:
            CodeGenDebug.LogError($"Failure occurred while attempting to post process assembly: \"{compiledAssembly.Name}\".");
            return null;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
