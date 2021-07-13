using System.Linq;
using System.IO;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using Microsoft.CodeAnalysis.Formatting;

namespace Unity.ClusterDisplay
{
    [InitializeOnLoad]
    public static class WrapperGenerator
    {
        static WrapperGenerator ()
        {
            RPCRegistry.onTriggerRecompile -= ProcessWrappers;
            RPCRegistry.onTriggerRecompile += ProcessWrappers;
        }

        private static MethodDeclarationSyntax CreateMethodDeclaration (System.Type returnType, System.Reflection.MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            MethodDeclarationSyntax methodDeclarationSyntax = null;
            if (returnType != null && returnType != typeof(void))
                methodDeclarationSyntax = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnType.FullName), methodInfo.Name);
            else methodDeclarationSyntax = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), methodInfo.Name);

            methodDeclarationSyntax = methodDeclarationSyntax.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
            var clusterRPCAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(typeof(ClusterRPC).Name));
            methodDeclarationSyntax = methodDeclarationSyntax.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(clusterRPCAttribute)));

            var memberAccessExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("instance"), SyntaxFactory.IdentifierName(methodInfo.Name));
            InvocationExpressionSyntax invocationExpression = null;

            if (parameters.Length == 0)
                invocationExpression = SyntaxFactory.InvocationExpression(memberAccessExpression);
            else
            {
                var separatedArgumentList = SyntaxFactory.SeparatedList<ArgumentSyntax>();
                var separatedParametersList = SyntaxFactory.ParameterList();

                for (int pi = 0; pi < parameters.Length; pi++)
                {
                    var newParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameters[pi].Name));
                    newParameter = newParameter.WithType(SyntaxFactory.ParseTypeName(parameters[pi].ParameterType.FullName));
                    separatedParametersList = separatedParametersList.AddParameters(newParameter);

                    var invocationArgument = SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameters[pi].Name));
                    separatedArgumentList = separatedArgumentList.Add(invocationArgument);
                }

                methodDeclarationSyntax = methodDeclarationSyntax.WithParameterList(separatedParametersList);
                var argumentList = SyntaxFactory.ArgumentList(separatedArgumentList);
                invocationExpression = SyntaxFactory.InvocationExpression(memberAccessExpression, argumentList);
            }

            StatementSyntax statementSyntax = null;
            if (returnType != null && returnType != typeof(void))
                statementSyntax = SyntaxFactory.ReturnStatement(invocationExpression);
            else statementSyntax = SyntaxFactory.ExpressionStatement(invocationExpression);

            methodDeclarationSyntax = methodDeclarationSyntax.WithBody(SyntaxFactory.Block(statementSyntax));
            return methodDeclarationSyntax;
        }

        public static void ProcessWrappers ()
        {
            if (!RPCSerializer.TryReadRPCStubs(RPCRegistry.RPCStubsPath, out var serializedInstanceRPCs, out var serializedStagedMethods))
                return;

            var baseComponent = typeof(Component);

            for (int i = 0; i < serializedInstanceRPCs.Length; i++)
            {
                if (serializedInstanceRPCs[i].isStatic)
                    continue;

                if (!RPCSerializer.TryDeserializeMethodInfo(serializedInstanceRPCs[i], out var rpcExecutionStage, out var methodInfo))
                    continue;

                var rpcDeclaringType = methodInfo.DeclaringType;
                if (rpcDeclaringType.IsAssignableFrom(baseComponent))
                    continue;

                var rpcParameters = methodInfo.GetParameters();
                var rpcReturnType = methodInfo.ReturnParameter;

                Microsoft.CodeAnalysis.SyntaxTree syntaxTree = null;
                var wrapperName = $"{rpcDeclaringType.Name}Wrapper";
                var folderPath = $"{Application.dataPath}/ClusterDisplay/Wrappers";
                var filePath = $"{folderPath}/{wrapperName}.cs";

                try
                {
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    if (File.Exists(filePath))
                    {
                        var text = File.ReadAllText(filePath);
                        syntaxTree = CSharpSyntaxTree.ParseText(text);
                    }
                }

                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                    return;
                }

                CompilationUnitSyntax compilationUnit = null;
                if (syntaxTree != null)
                {
                    compilationUnit = syntaxTree.GetCompilationUnitRoot();

                    var namespaceDeclaration = compilationUnit.Members.FirstOrDefault() as NamespaceDeclarationSyntax;
                    compilationUnit.Members.Remove(namespaceDeclaration);

                    var wrapperClassDeclaration = namespaceDeclaration.Members.FirstOrDefault() as ClassDeclarationSyntax;
                    namespaceDeclaration.Members.Remove(wrapperClassDeclaration);

                    var method = wrapperClassDeclaration.Members
                        .Where(memberDeclarationSyntax => memberDeclarationSyntax is MethodDeclarationSyntax)
                        .FirstOrDefault(memberDeclarationSyntax =>
                        {
                            var methodDeclrationSyntax = memberDeclarationSyntax as MethodDeclarationSyntax;
                            return
                                methodDeclrationSyntax.Identifier.Text == methodInfo.Name &&
                                methodDeclrationSyntax.ReturnType.ToString() == rpcReturnType.Name &&
                                methodDeclrationSyntax.ParameterList.Parameters.All(parameterSyntax => methodInfo.GetParameters().Any(parameter => parameter.ParameterType.Name == parameterSyntax.Type.ToString()));
                        });

                    if (method == null)
                    {
                        var methodDeclarationSyntax = CreateMethodDeclaration(rpcReturnType.ParameterType, methodInfo);
                        wrapperClassDeclaration = wrapperClassDeclaration.AddMembers(methodDeclarationSyntax);
                        namespaceDeclaration.Members.Add(wrapperClassDeclaration);
                        compilationUnit.Members.Add(namespaceDeclaration);
                    }
                }
                else
                {
                     compilationUnit = SyntaxFactory.CompilationUnit();

                    var componentWrapperType = typeof(ComponentWrapper<>);
                    var genericArguments = componentWrapperType.GetGenericArguments();
                    var genericTypeCountStr = $"`{genericArguments.Count()}";
                    var wrapperTypeName = $"{componentWrapperType.Name.Replace(genericTypeCountStr, "")}<{rpcDeclaringType.Name}>";

                    SyntaxList<UsingDirectiveSyntax> usingDirectives = new SyntaxList<UsingDirectiveSyntax>();
                    if (rpcDeclaringType.Namespace != null)
                        usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(rpcDeclaringType.Namespace)));
                    usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityEngine")));

                    var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("Unity.ClusterDisplay.Networking.Wrappers"));

                    var wrapperClassDeclaration = SyntaxFactory.ClassDeclaration(SyntaxFactory.ParseToken(wrapperName));

                    var requireComponent = typeof(RequireComponent);
                    var attributeArgument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({rpcDeclaringType.Name})"));
                    var requireComponentAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(requireComponent.Name), SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument)));
                    var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(requireComponentAttribute));

                    wrapperClassDeclaration = wrapperClassDeclaration.AddAttributeLists(attributeList);
                    wrapperClassDeclaration = wrapperClassDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                    wrapperClassDeclaration = wrapperClassDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(wrapperTypeName, consumeFullText: true)));
                    var methodDeclarationSyntax = CreateMethodDeclaration(rpcReturnType.ParameterType, methodInfo);
                    wrapperClassDeclaration = wrapperClassDeclaration.AddMembers(methodDeclarationSyntax);

                    namespaceDeclaration = namespaceDeclaration.AddMembers(wrapperClassDeclaration);
                    for (int ui = 0; ui < usingDirectives.Count; ui++)
                        compilationUnit = compilationUnit.AddUsings(usingDirectives[ui]);
                    compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);
                }

                var formattedCode = Formatter.Format(compilationUnit, new AdhocWorkspace());
                File.WriteAllText(filePath, formattedCode.ToFullString());
            }
        }
    }
}
