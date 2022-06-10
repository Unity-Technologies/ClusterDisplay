using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

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
        public (AttributeData clusterRPCAttrribute, MethodDeclarationSyntax method)[] rpcs => k_RPCsList.ToArray();
        readonly List<(AttributeData clusterRPCAttrribute, MethodDeclarationSyntax method)> k_RPCsList = new List<(AttributeData clusterRPCAttrribute, MethodDeclarationSyntax method)>();

        GeneratorExecutionContext m_Context;
        public SyntaxReceiver (GeneratorExecutionContext context) => m_Context = context;

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

                    var semanticModel = m_Context.Compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree);
                    var methodSymbolInfo = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                    var attributeData = methodSymbolInfo.GetAttributes().First();

                    k_RPCsList.Add((attributeData, methodDeclarationSyntax));
                    return;
                }
            }
        }
    }

    [Generator]
    public class RPCGenerator : ISourceGenerator
    {
        ExpressionSyntax GenerateMarshalSizeOfValueType(string typeName) =>
                SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(nameof(Marshal)),
                    SyntaxFactory.IdentifierName($"{nameof(Marshal.SizeOf)}<{typeName}>")));

        ExpressionSyntax GenerateMarshalSizeOfValueType(TypeInfo typeInfo) =>
            GenerateMarshalSizeOfValueType(typeInfo.Type.Name);

        ExpressionSyntax GenerateMarshalSizeOfValueType(ITypeSymbol typeSymbol) =>
            GenerateMarshalSizeOfValueType(typeSymbol.Name);

        ExpressionSyntax GenerateMarshalSizeOfValueType(TypeSyntax typeSyntax, TypeInfo typeInfo)
        {
            if (!typeInfo.Type.IsValueType)
            {
                throw new System.NullReferenceException($"Cannot determine size of non-value type: \"{typeInfo.Type.Name}\".");
            }

            return GenerateMarshalSizeOfValueType(typeInfo);
        }

        ExpressionSyntax GenerateSizeOfArrayElementType(GeneratorExecutionContext context, ParameterSyntax parameterSyntax)
        {
            var semanticModel = context.Compilation.GetSemanticModel(parameterSyntax.Type.SyntaxTree);
            var typeInfo = semanticModel.GetSpeculativeTypeInfo(0, parameterSyntax.Type, SpeculativeBindingOption.BindAsTypeOrNamespace);
            var arrayTypeSymbol = typeInfo.Type as IArrayTypeSymbol;

            return GenerateMarshalSizeOfValueType(arrayTypeSymbol.ElementType);
        }

        ExpressionSyntax GenerateGetArrayLength(ParameterSyntax parameterSyntax) =>
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(parameterSyntax.Identifier.Text),
                SyntaxFactory.IdentifierName("Length"));

        (ParameterSyntax parameter, ExpressionSyntax marshalSizeOf, bool dynamicSize) DetermineParameterInfo(GeneratorExecutionContext context, ParameterSyntax parameter)
        {
            var typeInfo = context.Compilation.GetSemanticModel(parameter.Type.SyntaxTree).GetTypeInfo(parameter.Type);
            if (typeInfo.Type.IsValueType)
            {
                return (parameter, GenerateMarshalSizeOfValueType(parameter.Type, typeInfo), false);
            }

            else if (parameter.Type.IsKind(SyntaxKind.ArrayType))
            {
                return (parameter, GenerateSizeOfArrayElementType(context, parameter), true);
            }

            /*
            else if (parameter.Type.IsKind(SyntaxKind.StringKeyword))
            {
                return (0, true);
                // dynamicallySizedParameter = true;
            }

            else if (typeInfo.Type.ContainingNamespace.Name == "Unity.Collections" && typeInfo.Type.Name == "NativeArray")
            {
                return (0, true);
            }
            */

            throw new System.NotImplementedException($"The following parameter: \"{parameter.Type} {parameter.Identifier}\" is unsupported");
        }

        (ParameterSyntax parameter, ExpressionSyntax marshalSizeOf, bool dynamicSize)[] DetermineAllParameterInfo(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.ParameterList.Parameters.Select(parameter => DetermineParameterInfo(context, parameter)).ToArray();

        ExpressionSyntax GenerateTotalParametersSizeArgument(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.ParseExpression("0" +
                DetermineAllParameterInfo(context, methodDeclarationSyntax)
                    .Aggregate(
                        "",
                        (aggregate, next) => next.dynamicSize ?
                            $" + /*Dynamic Block Size Header*/ Marshal.SizeOf<int>() + {next.marshalSizeOf} * {GenerateGetArrayLength(next.parameter)}" :
                            $" + {next.marshalSizeOf}")).NormalizeWhitespace();

        ArgumentSyntax GenerateRPCExecutionStageForRPCCall(GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax)
        {
            var semanticModel = context.Compilation.GetSemanticModel(methodDeclarationSyntax.SyntaxTree);
            var methodSymbolInfo = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
            var attributeData = methodSymbolInfo.GetAttributes().First();
            // var rpcExecutionStageArgument = clusterRPCAttribute.ArgumentList.Arguments.FirstOrDefault(argument => argument);
            return SyntaxFactory.Argument(SyntaxFactory.ParseName("0"));
                // SyntaxFactory.MemberAccessExpression(
                //     SyntaxKind.SimpleMemberAccessExpression,
                //     SyntaxFactory.IdentifierName("RPCExecutionStage"),
                //     SyntaxFactory.IdentifierName("Automatic")));
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


        BlockSyntax GenerateCaptureExecutionIfStatementBlock(GeneratorExecutionContext context, AttributeData clusterRPCAttribute, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
            {
                GenerateAppendRPCCall(context, clusterRPCAttribute, methodDeclarationSyntax),
                /*
                methodDeclarationSyntax.ParameterList.Parameters.Select<ParameterSyntax, StatementSyntax>(parameter =>
                {
                    var semanticModel = context.Compilation.GetSemanticModel(parameter.Type.SyntaxTree);
                    var typeInfo = semanticModel.GetTypeInfo(parameter.Type);

                    bool dynamicallySizedParameter;
                    int parameterByteSize = 0;

                    if (typeInfo.Type.IsValueType)
                    {
                        parameterByteSize = DetermineValueTypeByteSize(parameter.Type, typeInfo);
                        dynamicallySizedParameter = false;
                    }

                    else if (parameter.Type.IsKind(SyntaxKind.ArrayType))
                    {
                        dynamicallySizedParameter = true;
                        return SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("RPCBufferIO"),
                                    SyntaxFactory.IdentifierName("AppendRPCArrayParameterValues")),
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SeparatedList(new[]
                                    {
                                        SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameter.Identifier.Text))
                                    }))));
                    }

                    else if (parameter.Type.IsKind(SyntaxKind.StringKeyword))
                    {
                        dynamicallySizedParameter = true;
                    }

                    else if (typeInfo.Type.ContainingNamespace.Name == "Unity.Collections" && typeInfo.Type.Name == "NativeArray")
                    {
                        dynamicallySizedParameter = true;
                    }

                    return SyntaxFactory.EmptyStatement();
                }) as StatementSyntax*/
            }));

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
            MethodRewriter methodRewriter = new MethodRewriter();

            for (int mi = 0; mi < rpcs.Length; mi++)
            {
                try
                {
                    var type = rpcs[mi].method.Parent as TypeDeclarationSyntax;

                    var extensionClass = SyntaxFactory.ClassDeclaration(
                        attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                        modifiers: SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                        identifier: SyntaxFactory.ParseToken($"{type.Identifier}Extensions"),
                        typeParameterList: null,
                        baseList: null,
                        constraintClauses: SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                        members: new SyntaxList<MemberDeclarationSyntax>(
                            SyntaxFactory.MethodDeclaration(
                                attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                                rpcs[mi].method.ReturnType,
                                explicitInterfaceSpecifier: null,
                                identifier: SyntaxFactory.ParseToken($"Emit{rpcs[mi].method.Identifier}"),
                                typeParameterList: rpcs[mi].method.TypeParameterList != null ? rpcs[mi].method.TypeParameterList : null,
                                parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList<ParameterSyntax>(nodes: new[] {
                                    SyntaxFactory.Parameter(
                                        attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                                        modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword)),
                                        type: SyntaxFactory.ParseTypeName(type.Identifier.ToString()),
                                        identifier: SyntaxFactory.ParseToken("instance"),
                                        @default: null)
                                })).AddParameters(rpcs[mi].method.ParameterList.Parameters.ToArray()),
                                constraintClauses: rpcs[mi].method.ConstraintClauses,
                                body: SyntaxFactory.Block(SyntaxFactory.List<StatementSyntax>(new StatementSyntax[]
                                {
                                    GenerateCaptureExecutionIfStatement(context, rpcs[mi].clusterRPCAttrribute, rpcs[mi].method),
                                    rpcs[mi].method.ReturnType.IsKind(SyntaxKind.PredefinedType) &&
                                    ((rpcs[mi].method.ReturnType as PredefinedTypeSyntax).Keyword.ToString() == "void") ?
                                        SyntaxFactory.ExpressionStatement(GenerateInstanceMethodInvocation(context, rpcs[mi].method)) :
                                        SyntaxFactory.ReturnStatement(GenerateInstanceMethodInvocation(context, rpcs[mi].method)) as StatementSyntax
                                })),
                                semicolonToken: SyntaxFactory.Token(SyntaxKind.None)
                        )));

                    Debug.Log($"Extension class:\n{extensionClass.NormalizeWhitespace().GetText(encoding: Encoding.UTF8)}");

                    // var modifiedMethod = methodRewriter.Visit(methods[mi]);
                    // var newType = type.ReplaceNode(methods[mi], modifiedMethod);

                    // var compilationUnitRoot = type.SyntaxTree.GetCompilationUnitRoot(context.CancellationToken);
                    // var compilationUnit = SyntaxFactory.CompilationUnit(
                    //     compilationUnitRoot.Externs,
                    //     compilationUnitRoot.Usings,
                    //     compilationUnitRoot.AttributeLists,
                    //     compilationUnitRoot.Members.Replace(type, newType));

                    // var filePath = type.SyntaxTree.FilePath;
                    // var generated = compilationUnit.GetText(encoding: Encoding.UTF8);
                    // Debug.Log($"Writing the following generated code to: \"{filePath}\":\n{generated}");
                    // File.WriteAllText(filePath, compilationUnit.GetText(encoding: Encoding.UTF8).ToString());
                    // context.AddSource(filePath, generated);
                }

                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                    continue;
                }
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
