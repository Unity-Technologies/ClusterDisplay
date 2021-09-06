using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    /// <summary>
    /// This class asynchronously loads in recently compiled assemblies 
    /// and determines whether we should manipulate them.
    /// </summary>
    [InitializeOnLoad]
    public class AssemblyILPostProcessor : ILPostProcessor
    {
        // static AssemblyILPostProcessor () => UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

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
                string assemblyLocation = ReflectionUtils.GetAssemblyLocation(name.Name);
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
            Debug.Log($"Post processing assembly: \"{compiledAssembly.Name}\".");

            // Determine which assemblies we are allowed to manipulate.
            if (cachedRegisteredAssemblyFullNames == null)
                RPCSerializer.ReadRegisteredAssemblies(RPCRegistry.k_RegisteredAssembliesJsonPath, out cachedRegisteredAssemblyFullNames);

            // Determine whether the assembly we are about to process is allowed to be manipulated.
            AssemblyDefinition compiledAssemblyDef = null;
            if (!cachedRegisteredAssemblyFullNames.Any(registeredAssembly => registeredAssembly.Split(',')[0] == compiledAssembly.Name))
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
                Debug.LogException(exception);
                goto failure;
            }

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()));

            ignoreAssembly:
            return null;

            failure:
            Debug.LogError($"Failure occurred while attempting to post process assembly: \"{compiledAssembly.Name}\".");
            return null;
        }

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    }
}
