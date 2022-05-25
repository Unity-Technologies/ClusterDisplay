using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    [Generator]
    public class SourceGenerators : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            Console.WriteLine($"CompilationAssembly: \"{context.Compilation.AssemblyName}\".");
            context.AddSource("hellosource", SourceText.From("class HelloWorld {}"));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            Console.WriteLine("Initializing...");
            context.RegisterForSyntaxNotifications(() => new SynctaxReceiver());
        }
    }
}
