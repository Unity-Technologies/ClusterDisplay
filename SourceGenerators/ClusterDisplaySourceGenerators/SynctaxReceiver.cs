using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    internal class SynctaxReceiver : ISyntaxReceiver
    {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is NamespaceDeclarationSyntax)
            {
                NamespaceDeclarationSyntax namespaceDeclaration = syntaxNode as NamespaceDeclarationSyntax;
                Console.WriteLine($"Namespace: \"{namespaceDeclaration.Name.ToFullString()}\".");
            }

            if (syntaxNode is ClassDeclarationSyntax)
            {
                ClassDeclarationSyntax classDeclaration = syntaxNode as ClassDeclarationSyntax;
                Console.WriteLine($"Class: \"{classDeclaration.Identifier.ToFullString()}\".");
            }

            if (syntaxNode is MethodDeclarationSyntax)
            {
                var methodNode = syntaxNode as MethodDeclarationSyntax;
                bool isClusterRPC = false;

                Console.WriteLine($"Method: \"{methodNode.Identifier.ToFullString()}\".");

                foreach (var attributeNodeLists in methodNode.AttributeLists)
                {
                    foreach (var attribute in attributeNodeLists.Attributes)
                    {
                        Console.WriteLine($"Attribute: \"{attribute.Name.ToFullString()}\".");
                        if (attribute.Name.ToString() != "ClusterRPC")
                            continue;

                        isClusterRPC = true;
                        break;
                    }

                    if (isClusterRPC)
                        break;
                }

                if (isClusterRPC)
                    Console.WriteLine($"ClusterRPC: {(syntaxNode as MethodDeclarationSyntax).Identifier.ToFullString()}");

            }
        }
    }
}
