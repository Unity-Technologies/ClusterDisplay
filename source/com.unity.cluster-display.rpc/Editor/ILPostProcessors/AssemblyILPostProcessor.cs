#define CLUSTER_DISPLAY_ILPOSTPROCESSING

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        readonly Dictionary<string, string> m_CachedAssemblyPaths = new Dictionary<string, string>();
        readonly object cachedAssemblyPathLock = new object();
        public CecilUtils cecilUtils;
        CodeGenDebug logger;
        
        /// <summary>
        /// This class builds a path to the assembly location, loads the DLL bytes and
        /// converts those bytes into a Cecil AssemblyDefinition which we manipulate.
        /// </summary>
        internal sealed class AssemblyResolver : BaseAssemblyResolver
        {
            readonly CodeGenDebug logger;

            readonly Dictionary<string, string> m_CachedAssemblyPaths = new Dictionary<string, string>(); // Reference to parent instance.
            readonly Dictionary<string, AssemblyDefinition> cachedAssemblyDefinitions = new Dictionary<string, AssemblyDefinition>();

            public AssemblyResolver(CodeGenDebug logger, Dictionary<string, string> cachedAssemblyPaths)
            {
                this.m_CachedAssemblyPaths = cachedAssemblyPaths;
                this.logger = logger;
            }

            public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                AssemblyDefinition assemblyDefinition = null;
                if (cachedAssemblyDefinitions.TryGetValue(name.Name, out assemblyDefinition))
                    return assemblyDefinition;

                try
                {
                    string assemblyLocation = null;
                    lock (m_CachedAssemblyPaths)
                    {
                        if (!m_CachedAssemblyPaths.TryGetValue(name.Name, out assemblyLocation))
                        {
                            throw new System.ArgumentOutOfRangeException($"There is no known path for assembly: \"{name.Name}");
                        }
                    }
                    
                    parameters.AssemblyResolver = this;
                    parameters.SymbolStream = CreatePdbStreamFor(assemblyLocation);

                    var bytes = File.ReadAllBytes(assemblyLocation);
                    assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), parameters);
                    cachedAssemblyDefinitions.Add(name.Name, assemblyDefinition);

                    logger.Log($"Successfully read referenced assembly: \"{name.Name}\" at path: \"{assemblyLocation}\".");

                    return assemblyDefinition;
                }
                
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    return null;
                }
            }
        }

        internal sealed class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
        {
            public IReflectionImporter GetReflectionImporter(ModuleDefinition module) => new PostProcessorReflectionImporter(module);
            internal class PostProcessorReflectionImporter : DefaultReflectionImporter
            {
                const string SystemPrivateCoreLib = "System.Private.CoreLib";
                AssemblyNameReference _correctCorlib;
                
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
        public bool TryGetAssemblyDefinitionFor(ICompiledAssembly compiledAssembly, out AssemblyDefinition assemblyDef)
        {
            if (compiledAssembly.References.Length > 0)
            {
                string referencesMsg = $"{compiledAssembly.Name} references:";
                for (int i = 0; i < compiledAssembly.References.Length; i++)
                {
                    var referencePath = compiledAssembly.References[i];
                    var referenceName = Path.GetFileNameWithoutExtension(referencePath);

                    lock(cachedAssemblyPathLock)
                    {
                        if (m_CachedAssemblyPaths.ContainsKey(referenceName))
                            continue;

                        m_CachedAssemblyPaths.Add(referenceName, referencePath);
                        logger.Log($"Caching assembly reference {referenceName} at path: \"{referencePath}\".");
                    }
                    
                    referencesMsg += $"\n\t{referencePath}";
                }

                logger.Log(referencesMsg);
            }

            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(logger, m_CachedAssemblyPaths),
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
                logger.Log($"Successfully acquired assembly definition for target assembly.");
                return true;
            }

            catch (System.Exception exception)
            {
                logger.LogException(exception);
                assemblyDef = null;
                return false;
            }
        }

        /// <summary>
        /// Load in the assembly debugging information if it exists.
        /// </summary>
        /// <param name="assemblyLocation"></param>
        /// <returns></returns>
        static MemoryStream CreatePdbStreamFor(string assemblyLocation)
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
            logger = new CodeGenDebug(compiledAssembly.Name, true);
            logger.Log($"Polling assembly.");

            // Get the Cecil AssemblyDefinition for the assembly.
            AssemblyDefinition compiledAssemblyDef;
            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out compiledAssemblyDef))
                goto failure;

            MemoryStream pe, pdb;

            try
            {
                // Now we manipulate the assembly's IL.
                cecilUtils = new CecilUtils(logger);
                RPCILPostProcessor postProcessor = new RPCILPostProcessor(cecilUtils, logger);
                
                // Returns false if the assembly was not modfiied.
                logger.Log($"Starting post process on assembly.");
                if (!postProcessor.Execute(compiledAssemblyDef))
                    goto ignoreAssembly;

                pe = new MemoryStream();
                pdb = new MemoryStream();

                var writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(), 
                    SymbolStream = pdb, 
                    WriteSymbols = true,
                };

                logger.Log($"Attempting to write modified assembly to disk.");
                compiledAssemblyDef.Write(pe, writerParameters);
            }

            catch (System.Exception exception)
            {
                logger.LogException(exception);
                goto failure;
            }

            logger.Log($"Finished assembly.");
            // done = true;
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));

            ignoreAssembly:
            // done = true;
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly);

            failure:
            logger.LogError($"Failure occurred while attempting to post process assembly: \"{compiledAssembly.Name}\".");
            // done = true;
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly);
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
