#define CLUSTER_DISPLAY_ILPOSTPROCESSING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    
    /// <summary>
    /// This class asynchronously loads in recently compiled assemblies 
    /// and determines whether we should manipulate them.
    /// </summary>
    internal class AssemblyILPostProcessor : ILPostProcessor
    {
        private static readonly Dictionary<string, string> cachedAssemblyPath = new Dictionary<string, string>();
        
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

            private readonly Dictionary<string, AssemblyDefinition> cachedAssemblyDefinitions = new Dictionary<string, AssemblyDefinition>();

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (cachedAssemblyDefinitions.TryGetValue(name.Name, out var assemblyDefinition))
                    return assemblyDefinition;

                try
                {
                    if (!cachedAssemblyPath.TryGetValue(name.Name, out var assemblyLocation))
                        throw new Exception($"There is no known path for assembly: \"{name.Name}");
                    
                    parameters.AssemblyResolver = this;
                    parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                    assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assemblyLocation)), parameters);
                    cachedAssemblyDefinitions.Add(name.Name, assemblyDefinition);
                    return assemblyDefinition;

                }
                
                catch (AssemblyResolutionException ex)
                {
                    CodeGenDebug.LogException(ex);
                    return null;
                }
            }
        }

        internal sealed class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
        {
            public IReflectionImporter GetReflectionImporter(ModuleDefinition module) => new PostProcessorReflectionImporter(module);
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
        }

        /// <summary>
        /// Loads the assembly into memory and builds a Cecil AssemblyDefinition.
        /// </summary>
        /// <param name="compiledAssembly"></param>
        /// <param name="assemblyDef"></param>
        /// <returns></returns>
        public static bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
        {
            if (compiledAssembly.References.Length > 0)
            {
                string referencesMsg = $"{compiledAssembly.Name} references:";
                for (int i = 0; i < compiledAssembly.References.Length; i++)
                {
                    var referencePath = compiledAssembly.References[i];
                    var referenceName = Path.GetFileNameWithoutExtension(referencePath);
                    cachedAssemblyPath.Add(referenceName, referencePath);
                    
                    referencesMsg = $"{referencesMsg}\n\t{referencePath}";
                }
                CodeGenDebug.Log(referencesMsg);
            }
            
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(),
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                // MetadataImporterProvider = new CoreLibMetadataImporterProvider(),
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
            CodeGenDebug.BeginILPostProcessing(compiledAssembly.Name);
            CodeGenDebug.Log($"Polling assembly.");
            

            // Get the Cecil AssemblyDefinition for the assembly.
            AssemblyDefinition compiledAssemblyDef;
            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out compiledAssemblyDef))
                goto failure;

            MemoryStream pe, pdb;

            try
            {
                // Now we manipulate the assembly's IL.
                RPCILPostProcessor postProcessor = new RPCILPostProcessor();
                
                // Returns false if the assembly was not modfiied.
                CodeGenDebug.Log($"Starting post process on assembly.");
                if (!postProcessor.Execute(compiledAssemblyDef))
                    goto ignoreAssembly;
                ;
                pe = new MemoryStream();
                pdb = new MemoryStream();

                var writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(), 
                    SymbolStream = pdb, 
                    WriteSymbols = true,
                };

                CodeGenDebug.Log($"Attempting to write modified assembly to disk.");
                compiledAssemblyDef.Write(pe, writerParameters);
            }

            catch (System.Exception exception)
            {
                CodeGenDebug.LogException(exception);
                goto failure;
            }

            CodeGenDebug.Log($"Finished assembly.");
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
