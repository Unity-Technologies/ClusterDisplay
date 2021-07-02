using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis;
using UnityEditor.Compilation;
using System.Reflection;
using System.Collections.Generic;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.ClusterDisplay.Networking
{
    [InitializeOnLoad]
    public class WrapperSourceGenerator
    {
        static WrapperSourceGenerator ()
        {
            // AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReloaded;
            // AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReloaded;
            RPCRegistry.onTriggerRecompile -= Generate;
            RPCRegistry.onTriggerRecompile += Generate;
        }

        // private static void OnBeforeAssemblyReloaded()
        // {
        //     Generate();
        // }

        public static void Generate ()
        {
            string wrappersFolderPath = "./Assets/ClusterDisplay/Wrappers/";

            var rpcs = RPCRegistry.CopyRPCs();
            Dictionary<Type, List<MethodInfo>> methods = new Dictionary<Type, List<MethodInfo>>();
            List<Type> types = new List<Type>();

            for (int i = 0; i < rpcs.Length; i++)
            {
                if (ReflectionUtils.TypeIsInPostProcessableAssembly(rpcs[i].methodInfo.DeclaringType))
                    continue;
                if (methods.TryGetValue(rpcs[i].methodInfo.DeclaringType, out var typeMethods))
                    typeMethods.Add(rpcs[i].methodInfo);
                else methods.Add(rpcs[i].methodInfo.DeclaringType, new List<MethodInfo>() { rpcs[i].methodInfo });
            }

            try
            {
                if (methods.Count == 0)
                {
                    if (Directory.Exists(wrappersFolderPath))
                        Directory.Delete(wrappersFolderPath);
                    return;
                }

                if (!Directory.Exists(wrappersFolderPath))
                    Directory.CreateDirectory(wrappersFolderPath);

                else
                {
                    var wrapperFiles = Directory.GetFiles(wrappersFolderPath);
                    for (int i = 0; i < wrapperFiles.Length; i++)
                        File.Delete(wrapperFiles[i]);
                }
            }

            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            var compilationUnitSyntax = SyntaxFactory.CompilationUnit();
            var namespaceSyntaxTree = compilationUnitSyntax.AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("Unity.ClusterDisplay.Networking.Wrappers")));

            foreach (var typeMethods in methods)
            {
                var classSyntaxTree = SyntaxFactory.ClassDeclaration($"{typeMethods.Key.Name}Wrapper");
                foreach (var method in typeMethods.Value)
                {
                    var methodDeclaration = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(method.ReturnType.Name), method.Name);
                    methodDeclaration
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                        .WithBody(SyntaxFactory.Block());

                    var parameters = method.GetParameters();
                    if (parameters.Length > 0)
                    {
                        var parameterList = SyntaxFactory.ParameterList();
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameters[i].Name));
                            parameter.WithType(SyntaxFactory.ParseTypeName(parameters[i].ParameterType.Name));
                            parameterList.AddParameters(parameter);
                        }
                    }

                    classSyntaxTree.AddMembers(methodDeclaration);
                }

                namespaceSyntaxTree.AddMembers(classSyntaxTree);
            }

            var workspace = new AdhocWorkspace();
            var formattedCode = Formatter.Format(compilationUnitSyntax, workspace);

            try
            {
                File.WriteAllText($"{wrappersFolderPath}/TestClass.cs", formattedCode.ToFullString());
                File.WriteAllText($"{wrappersFolderPath}/Unity.ClusterDisplay.Networking.Wrappers.asmdef", "{\r\t\"name\" : \"Unity.CLusterDisplay.Networking.Wrappers\"\r}");
            }

            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
