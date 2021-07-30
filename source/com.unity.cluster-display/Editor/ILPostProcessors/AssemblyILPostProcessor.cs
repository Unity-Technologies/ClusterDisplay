using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    public class AssemblyILPostProcessor : ILPostProcessor
    {
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

        public static bool TryGetAssemblyDefinitionFor(Assembly compiledAssembly, bool readSymbols, out AssemblyDefinition assemblyDef)
        {
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = new AssemblyResolver(),
                // SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = readSymbols ? new PortablePdbReaderProvider() : null,
                ReadSymbols = readSymbols,
                ReadingMode = ReadingMode.Immediate,
            };

            // var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            try
            {
                assemblyDef = AssemblyDefinition.ReadAssembly(compiledAssembly.Location, readerParameters);
                return true;
            } catch (System.Exception exception)
            {
                Debug.LogException(exception);
                assemblyDef = null;
                return false;
            }
        }

        private static MemoryStream CreatePdbStreamFor(string assemblyLocation)
        {
            string pdbFilePath = Path.ChangeExtension(assemblyLocation, ".pdb");
            return !File.Exists(pdbFilePath) ? null : new MemoryStream(File.ReadAllBytes(pdbFilePath));
        }

        public override ILPostProcessor GetInstance() => this;
        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (cachedRegisteredAssemblyFullNames == null)
                RPCSerializer.ReadRegisteredAssemblies(RPCRegistry.k_RegisteredAssembliesJsonPath, out cachedRegisteredAssemblyFullNames);

            AssemblyDefinition compiledAssemblyDef = null;
            if (!cachedRegisteredAssemblyFullNames.Any(registeredAssembly => registeredAssembly.Split(',')[0] == compiledAssembly.Name))
                goto ignoreAssembly;

            if (!TryGetAssemblyDefinitionFor(compiledAssembly, out compiledAssemblyDef))
                goto failure;

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
