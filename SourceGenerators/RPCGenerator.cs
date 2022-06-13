using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    public class MethodRewriter : CSharpSyntaxRewriter
    {
        bool IsCaptureExecution (StatementSyntax statementSyntax)
        {
            var firstStatement = statementSyntax as IfStatementSyntax;

            if (firstStatement == null)
            {
                return false;
            }

            var memberAccessExpression = firstStatement.Condition as MemberAccessExpressionSyntax;
            if (memberAccessExpression == null)
            {
                return false;
            }

            if (memberAccessExpression.Expression.ToString() != "RPCBufferIO" || memberAccessExpression.Name.ToString() != "CaptureExecution")
            {
                return false;
            }

            return true;
        }

        BlockSyntax GenerateCaptureExecutionIfStatementBlock (MethodDeclarationSyntax methodDeclarationSyntax)
        {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            return SyntaxFactory.Block(SyntaxFactory.List(statements));
        }

        IfStatementSyntax GenerateCaptureExecutionIfStatement (MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.IfStatement(
                        SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("RPCBufferIO"), SyntaxFactory.IdentifierName("CaptureExecution")),
                        GenerateCaptureExecutionIfStatementBlock(methodDeclarationSyntax));


        MethodDeclarationSyntax ModifyMethod (MethodDeclarationSyntax methodDeclarationSyntax)
        {
            Debug.Log($"Modifying method: \"{methodDeclarationSyntax.Identifier}\".");

            if (methodDeclarationSyntax.Body == null)
            {
                bool isVoid =
                    methodDeclarationSyntax.ReturnType.IsKind(SyntaxKind.PredefinedType) &&
                    (methodDeclarationSyntax.ReturnType as PredefinedTypeSyntax).Keyword.ToString() == "void";

                if (isVoid)
                {
                    return methodDeclarationSyntax.WithBody(SyntaxFactory.Block(SyntaxFactory.List(
                        new []
                        {
                            GenerateCaptureExecutionIfStatement(methodDeclarationSyntax),
                            SyntaxFactory.ExpressionStatement(methodDeclarationSyntax.ExpressionBody.Expression) as StatementSyntax
                        })));
                }
            }

            var statements = methodDeclarationSyntax.Body.Statements;

            if (statements.Count == 0)
            {
                return methodDeclarationSyntax;
            }

            List<StatementSyntax> statementsToRemove = new List<StatementSyntax>();
            for (int i = 0; i < statements.Count; i++)
            {
                if (IsCaptureExecution(statements[i]))
                {
                    statementsToRemove.Add(statements[i]);
                }
            }

            for (int i = 0; i < statementsToRemove.Count; i++)
            {
                statements = statements.Remove(statementsToRemove[i]);
            }

            var firstStatement = statements[0];
            if (IsCaptureExecution(firstStatement))
            {
                statements = statements.Replace(firstStatement, GenerateCaptureExecutionIfStatement(methodDeclarationSyntax));
            }

            else
            {
                statements = statements.Insert(0, GenerateCaptureExecutionIfStatement(methodDeclarationSyntax));
            }

            return methodDeclarationSyntax.WithBody(SyntaxFactory.Block(statements));
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax methodNode)
        {
            return ModifyMethod(methodNode);
        }
    }

    public class SyntaxReceiver : ISyntaxReceiver
    {
        public MethodDeclarationSyntax[] rpcs => k_RPCsList.ToArray();
        readonly List<MethodDeclarationSyntax> k_RPCsList = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            var methodDeclarationSyntax = syntaxNode as MethodDeclarationSyntax;
            if (methodDeclarationSyntax == null)
            {
                return;
            }

            // Debug.Log($"Inspecting method: \"{methodNode.Identifier.ToFullString()}\" at path: \"{methodNode.SyntaxTree.FilePath}\".");

            foreach (var attributeNodeLists in methodDeclarationSyntax.AttributeLists)
            {
                foreach (var attribute in attributeNodeLists.Attributes)
                {
                    if (attribute.Name.ToString() != "ClusterRPC")
                        continue;

                    k_RPCsList.Add(methodDeclarationSyntax);
                    return;
                }
            }
        }
    }

    [Generator]
    public class RPCGenerator : ISourceGenerator
    {
        ExpressionSyntax GenerateMarshalSizeOf(string typeName) =>
                SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(nameof(Marshal)),
                    SyntaxFactory.IdentifierName($"{nameof(Marshal.SizeOf)}<{typeName}>")));

        ExpressionSyntax GenerateMarshalSizeOf<T>() =>
                SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(nameof(Marshal)),
                    SyntaxFactory.IdentifierName($"{nameof(Marshal.SizeOf)}<{typeof(T).Name}>")));

        ExpressionSyntax GenerateMarshalSizeOfValueType(TypeInfo typeInfo) =>
            GenerateMarshalSizeOf(typeInfo.Type.Name);

        ExpressionSyntax GenerateMarshalSizeOfValueType(ITypeSymbol typeSymbol) =>
            GenerateMarshalSizeOf(typeSymbol.Name);

        ExpressionSyntax GenerateMarshalSizeOf(TypeInfo typeInfo) =>
            typeInfo.Type.IsValueType ?
                GenerateMarshalSizeOfValueType(typeInfo) :
                throw new System.NullReferenceException($"Cannot determine size of non-value type: \"{typeInfo.Type.Name}\".");

        ExpressionSyntax GenerateMarshalSizeOfNativeArray(GeneratorExecutionContext context, TypeSyntax typeSyntax)
        {
            var typeArgument = (typeSyntax as GenericNameSyntax).TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArgument == null)
            {
                throw new System.NullReferenceException($"Unable to retrieve NativeArray type parameter.");
            }

            return GenerateMarshalSizeOfValueType(context.Compilation.GetSemanticModel(typeArgument.SyntaxTree).GetTypeInfo(typeSyntax, context.CancellationToken));
        }

        ExpressionSyntax GenerateMarshalSizeOfArrayElementType(GeneratorExecutionContext context, ParameterSyntax parameterSyntax)
        {
            var semanticModel = context.Compilation.GetSemanticModel(parameterSyntax.Type.SyntaxTree);
            var typeInfo = semanticModel.GetSpeculativeTypeInfo(0, parameterSyntax.Type, SpeculativeBindingOption.BindAsTypeOrNamespace);
            var arrayTypeSymbol = typeInfo.Type as IArrayTypeSymbol;

            return GenerateMarshalSizeOfValueType(arrayTypeSymbol.ElementType);
        }

        ExpressionSyntax GenerateGetLength(ParameterSyntax parameterSyntax) =>
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(parameterSyntax.Identifier.Text),
                SyntaxFactory.IdentifierName("Length"));

        (ParameterSyntax parameter, ExpressionSyntax marshalSizeOf, bool dynamicSize) DetermineParameterInfo(GeneratorExecutionContext context, ParameterSyntax parameter)
        {
            var typeInfo = context.Compilation.GetSemanticModel(parameter.Type.SyntaxTree).GetTypeInfo(parameter.Type);

            if (typeInfo.Type.IsValueType)
            {
                if (typeInfo.Type.ContainingNamespace.Name == "Unity.Collections" && typeInfo.Type.Name == "NativeArray")
                {
                    return (parameter, GenerateMarshalSizeOfNativeArray(context, parameter.Type), true);
                }

                return (parameter, GenerateMarshalSizeOfValueType(typeInfo), true);
            }

            else if (parameter.Type.IsKind(SyntaxKind.ArrayType))
            {
                return (parameter, GenerateMarshalSizeOfArrayElementType(context, parameter), true);
            }

            else if (typeInfo.Type.SpecialType == SpecialType.System_String)
            {
                return (parameter, SyntaxFactory.ParseExpression("sizeof(char)"), true);
            }

            throw new System.NotImplementedException($"The following parameter: \"{parameter.Type} {parameter.Identifier}\" is unsupported");
        }

        (ParameterSyntax parameter, ExpressionSyntax marshalSizeOf, bool dynamicSize)[] DetermineAllParameterInfo(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.ParameterList.Parameters.Select(parameter => DetermineParameterInfo(context, parameter)).ToArray();

        ExpressionSyntax GenerateTotalParametersSizeArgument(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.ParseExpression("0" +
                DetermineAllParameterInfo(context, methodDeclarationSyntax)
                    .Aggregate(
                        "",
                        (aggregate, next) => aggregate + (next.dynamicSize ?
                            $" + {GenerateMarshalSizeOf<int>()} + {next.marshalSizeOf} * {GenerateGetLength(next.parameter)}" :
                            $" + {next.marshalSizeOf}"))).NormalizeWhitespace();

        ArgumentSyntax GenerateRPCExecutionStageForRPCCall(GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax)
        {
            var rpcExecutionStageConstructorArgument = clusterRPCAttribute.ConstructorArguments.FirstOrDefault(constructorArgument => constructorArgument.Type.Name == "RPCExecutionStage");
            if (rpcExecutionStageConstructorArgument.IsNull)
            {
                throw new System.NullReferenceException($"Cluster RPC attribute is missing a RPCExecutionStage constructor argument.");
            }

            return SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{rpcExecutionStageConstructorArgument.Value}"));
        }

        StatementSyntax GenerateAppendRPCCall(GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.ExpressionStatement(
                methodDeclarationSyntax.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.StaticKeyword)) ?
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("RPCBufferIO"),
                            SyntaxFactory.IdentifierName("AppendStaticRPCCall")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(new[]
                                {
                                    GenerateRPCExecutionStageForRPCCall(context, clusterRPCAttribute, methodDeclarationSyntax),
                                    SyntaxFactory.Argument(GenerateTotalParametersSizeArgument(context, methodDeclarationSyntax)),
                                }))) :
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("RPCBufferIO"),
                            SyntaxFactory.IdentifierName("AppendRPCCall")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(new[]
                                {
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("instance")),
                                    GenerateRPCExecutionStageForRPCCall(context, clusterRPCAttribute, methodDeclarationSyntax),
                                    SyntaxFactory.Argument(GenerateTotalParametersSizeArgument(context, methodDeclarationSyntax)),
                                }))));

        StatementSyntax GenerateAppendRPCExpression (ParameterSyntax parameter, string appendRPCMethodName) =>
            SyntaxFactory.ExpressionStatement(
               SyntaxFactory.InvocationExpression(
                   SyntaxFactory.MemberAccessExpression(
                       SyntaxKind.SimpleMemberAccessExpression,
                       SyntaxFactory.IdentifierName("RPCBufferIO"),
                       SyntaxFactory.IdentifierName(appendRPCMethodName)),
                   SyntaxFactory.ArgumentList(
                       SyntaxFactory.SeparatedList(new[]
                       {
                           SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameter.Identifier.Text))
                       }))));

        StatementSyntax GenerateAppendRPCValueTypeExpression(ParameterSyntax parameter) =>
            GenerateAppendRPCExpression(parameter, "AppendRPCValueTypeParameterValue");

        StatementSyntax GenerateAppendRPCArrayExpression (ParameterSyntax parameter) =>
            GenerateAppendRPCExpression(parameter, "AppendRPCArrayParameterValues");

        StatementSyntax GenerateAppendRPCStringExpression (ParameterSyntax parameter) =>
            GenerateAppendRPCExpression(parameter, "AppendRPCStringParameterValues");

        StatementSyntax GenerateAppendRPCNativeArrayExpression (ParameterSyntax parameter) =>
            GenerateAppendRPCExpression(parameter, "AppendRPCNativeArrayParameterValues");

        StatementSyntax GenerateAppendRPCParameterValue (GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax, ParameterSyntax parameter)
        {
            var semanticModel = context.Compilation.GetSemanticModel(parameter.Type.SyntaxTree);
            var typeInfo = semanticModel.GetTypeInfo(parameter.Type);

            if (typeInfo.Type.IsValueType)
            {
                return GenerateAppendRPCValueTypeExpression(parameter);
            }

            else if (parameter.Type.IsKind(SyntaxKind.ArrayType))
            {
                return GenerateAppendRPCArrayExpression(parameter);
            }

            else if (typeInfo.Type.SpecialType == SpecialType.System_String)
            {
                return GenerateAppendRPCStringExpression(parameter);
            }

            else if (typeInfo.Type.ContainingNamespace.Name == "Unity.Collections" && typeInfo.Type.Name == "NativeArray")
            {
                return GenerateAppendRPCNativeArrayExpression(parameter);
            }

            throw new System.NotImplementedException($"Unable to generate source to emit argument: \"{parameter.Identifier}\" for method: \"{methodDeclarationSyntax.Identifier}\".");
        }

        SeparatedSyntaxList<StatementSyntax> GenerateAppendRPCParameterValues(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
                SyntaxFactory.SeparatedList(methodDeclarationSyntax.ParameterList.Parameters
                    .Select(parameter => GenerateAppendRPCParameterValue(context, methodDeclarationSyntax, parameter)));

        BlockSyntax GenerateCaptureExecutionIfStatementBlock(GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.Block(SyntaxFactory.List(new[]
            {
                GenerateAppendRPCCall(context, clusterRPCAttribute, methodDeclarationSyntax),
            }.Concat(GenerateAppendRPCParameterValues(context, methodDeclarationSyntax))));

        IfStatementSyntax GenerateCaptureExecutionIfStatement (GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.IfStatement(
                        SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("RPCBufferIO"), SyntaxFactory.IdentifierName("CaptureExecution")),
                        GenerateCaptureExecutionIfStatementBlock(context, clusterRPCAttribute, methodDeclarationSyntax));

        InvocationExpressionSyntax GenerateInstanceMethodInvocation(GeneratorExecutionContext context, MethodDeclarationSyntax method) =>
            SyntaxFactory.InvocationExpression(
                expression: SyntaxFactory.MemberAccessExpression(
                    kind: SyntaxKind.SimpleMemberAccessExpression,
                    expression: SyntaxFactory.IdentifierName("instance"),
                    name: SyntaxFactory.IdentifierName(method.Identifier)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(method.ParameterList.Parameters.Select(parameter =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier))
                ).ToArray())));

        void Generate (GeneratorExecutionContext context)
        {
            var syntaxReceiver = context.SyntaxReceiver as SyntaxReceiver;

            var rpcs = syntaxReceiver.rpcs;
            if (rpcs.Length == 0)
            {
                return;
            }

            Dictionary<string, (TypeDeclarationSyntax targetType, ClassDeclarationSyntax extensionClass)> generatedExtensions = new Dictionary<string, (TypeDeclarationSyntax targetType, ClassDeclarationSyntax extensionClass)>();

            for (int mi = 0; mi < rpcs.Length; mi++)
            {
                var semanticModel = context.Compilation.GetSemanticModel(rpcs[mi].SyntaxTree);
                var methodSymbolInfo = semanticModel.GetDeclaredSymbol(rpcs[mi]);
                var attributeData = methodSymbolInfo.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass.Name == "ClusterRPC");

                try
                {
                    var type = rpcs[mi].Parent as TypeDeclarationSyntax;
                    string extensionClassName = $"{type.Identifier}Extensions";
                    bool extensionClassExists = generatedExtensions.TryGetValue(extensionClassName, out var extensionClass);
                    if (!extensionClassExists)
                    {
                        extensionClass = (type, SyntaxFactory.ClassDeclaration(
                            attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                            modifiers: SyntaxFactory.TokenList(
                                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                            identifier: SyntaxFactory.ParseToken(extensionClassName),
                            typeParameterList: null,
                            baseList: null,
                            constraintClauses: SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                            members: SyntaxFactory.List<MemberDeclarationSyntax>()));
                    }

                    extensionClass.extensionClass = extensionClass.extensionClass.AddMembers(
                            SyntaxFactory.MethodDeclaration(
                                attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                                rpcs[mi].ReturnType,
                                explicitInterfaceSpecifier: null,
                                identifier: SyntaxFactory.ParseToken($"Emit{rpcs[mi].Identifier}"),
                                typeParameterList: rpcs[mi].TypeParameterList != null ? rpcs[mi].TypeParameterList : null,
                                parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList<ParameterSyntax>(nodes: new[] {
                                    SyntaxFactory.Parameter(
                                        attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                                        modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword)),
                                        type: SyntaxFactory.ParseTypeName(type.Identifier.ToString()),
                                        identifier: SyntaxFactory.ParseToken("instance"),
                                        @default: null)
                                })).AddParameters(rpcs[mi].ParameterList.Parameters.ToArray()),
                                constraintClauses: rpcs[mi].ConstraintClauses,
                                body: SyntaxFactory.Block(SyntaxFactory.List<StatementSyntax>(new StatementSyntax[]
                                {
                                    GenerateCaptureExecutionIfStatement(context, attributeData, rpcs[mi]),
                                    rpcs[mi].ReturnType.IsKind(SyntaxKind.PredefinedType) &&
                                    ((rpcs[mi].ReturnType as PredefinedTypeSyntax).Keyword.ToString() == "void") ?
                                        SyntaxFactory.ExpressionStatement(GenerateInstanceMethodInvocation(context, rpcs[mi])) :
                                        SyntaxFactory.ReturnStatement(GenerateInstanceMethodInvocation(context, rpcs[mi])) as StatementSyntax
                                })),
                                semicolonToken: SyntaxFactory.Token(SyntaxKind.None)));

                    if (!extensionClassExists)
                    {
                        generatedExtensions.Add(extensionClassName, extensionClass);
                    }

                    else
                    {
                        generatedExtensions[extensionClassName] = extensionClass;
                    }
                }

                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                    continue;
                }

            }

            foreach (var generated in generatedExtensions)
            {
                var compilationUnitRoot = generated.Value.targetType.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken);
                var compilationUnit = SyntaxFactory.CompilationUnit(
                    compilationUnitRoot.Externs,
                    compilationUnitRoot.Usings.AddRange(new[]
                    {
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Runtime.InteropServices")),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Unity.ClusterDisplay.RPC")),
                    }),
                    compilationUnitRoot.AttributeLists,
                    SyntaxFactory.List(new[]
                    {

                        (generated.Value.targetType.Parent is NamespaceDeclarationSyntax namespaceDeclarationSyntax) ?
                            SyntaxFactory.NamespaceDeclaration(
                                name: namespaceDeclarationSyntax.Name,
                                externs: SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
                                usings: SyntaxFactory.List<UsingDirectiveSyntax>(),
                                members: SyntaxFactory.List(new [] { generated.Value.extensionClass as MemberDeclarationSyntax })) :
                            generated.Value.extensionClass as MemberDeclarationSyntax

                    }));

                var generatedCode = compilationUnit.NormalizeWhitespace().GetText(encoding: Encoding.UTF8);
                Debug.Log($"Generated extension class for type: \"{generated.Value.targetType.Identifier}\n{generatedCode}");
                context.AddSource($"{generated.Value.targetType.Identifier}.Generated", generatedCode);
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                Generate(context);
            }

            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void Initialize(GeneratorInitializationContext context) => context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }
}
