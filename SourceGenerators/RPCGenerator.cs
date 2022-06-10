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
        public MethodDeclarationSyntax[] methods => k_MethodList.ToArray();
        readonly List<MethodDeclarationSyntax> k_MethodList = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            var methodNode = syntaxNode as MethodDeclarationSyntax;
            if (methodNode == null)
            {
                return;
            }

            // Debug.Log($"Inspecting method: \"{methodNode.Identifier.ToFullString()}\" at path: \"{methodNode.SyntaxTree.FilePath}\".");

            foreach (var attributeNodeLists in methodNode.AttributeLists)
            {
                foreach (var attribute in attributeNodeLists.Attributes)
                {
                    if (attribute.Name.ToString() != "ClusterRPC")
                        continue;

                    k_MethodList.Add(methodNode);
                    return;
                }
            }
        }
    }

    [Generator]
    public class RPCGenerator : ISourceGenerator
    {
        int DetermineSizeOfPredefinedValueType (TypeInfo typeInfo)
        {
            switch (typeInfo.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return Marshal.SizeOf<bool>();
                case SpecialType.System_Char:
                    return Marshal.SizeOf<char>();
                case SpecialType.System_SByte:
                    return Marshal.SizeOf<sbyte>();
                case SpecialType.System_Byte:
                    return Marshal.SizeOf<byte>();
                case SpecialType.System_Int16:
                    return Marshal.SizeOf<short>();
                case SpecialType.System_UInt16:
                    return Marshal.SizeOf<ushort>();
                case SpecialType.System_Int32:
                    return Marshal.SizeOf<int>();
                case SpecialType.System_UInt32:
                    return Marshal.SizeOf<uint>();
                case SpecialType.System_Int64:
                    return Marshal.SizeOf<long>();
                case SpecialType.System_UInt64:
                    return Marshal.SizeOf<ulong>();
                case SpecialType.System_Decimal:
                    return Marshal.SizeOf<decimal>();
                case SpecialType.System_Single:
                    return Marshal.SizeOf<float>();
                case SpecialType.System_Double:
                    return Marshal.SizeOf<double>();
                default:
                    throw new System.NotImplementedException($"The following type: \"{typeInfo.Type.SpecialType}\"");
            }
        }

        int DetermineValueTypeByteSize (TypeSyntax typeSyntax, TypeInfo typeInfo)
        {
            if (!typeInfo.Type.IsValueType)
            {
                throw new System.NullReferenceException($"Cannot determine size of non-value type: \"{typeInfo.Type.Name}\".");
            }

            if (typeSyntax.IsKind(SyntaxKind.PredefinedType))
            {
                return DetermineSizeOfPredefinedValueType(typeInfo);
            }

            return 0;
        }

        int DetermineByteSizeOfArrayElementValueType (GeneratorExecutionContext context, ParameterSyntax parameterSyntax)
        {
            var typeInfo = context.Compilation.GetSemanticModel(parameterSyntax.Type.SyntaxTree).GetTypeInfo(parameterSyntax.Type);
            var arrayTypeSymbol = typeInfo.Type as IArrayTypeSymbol;

            if (!arrayTypeSymbol.ElementType.IsValueType)
            {
                throw new System.NotImplementedException($"Cannot determine byte size of non-value array element types.");
            }

            return DetermineValueTypeByteSize(
                SyntaxFactory.ParseName(arrayTypeSymbol.ElementType.Name),
                context.Compilation.GetSemanticModel(parameterSyntax.Type.SyntaxTree)
                    .GetTypeInfo(SyntaxFactory.ParseName(arrayTypeSymbol.ElementType.Name)));
        }

        ExpressionSyntax GenerateGetArrayLength(ParameterSyntax parameterSyntax) =>
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(parameterSyntax.Identifier.Text),
                SyntaxFactory.IdentifierName("Length"));

        (ParameterSyntax parameter, int elementByteSize, bool dynamicSize) DetermineParameterInfo (GeneratorExecutionContext context, ParameterSyntax parameter)
        {
            var typeInfo = context.Compilation.GetSemanticModel(parameter.Type.SyntaxTree).GetTypeInfo(parameter.Type);
            if (typeInfo.Type.IsValueType)
            {
                return (parameter, DetermineValueTypeByteSize(parameter.Type, typeInfo), false);
            }

            else if (parameter.Type.IsKind(SyntaxKind.ArrayType))
            {
                return (parameter, DetermineByteSizeOfArrayElementValueType(context, parameter), true);
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

        (ParameterSyntax parameter, int elementByteSize, bool dynamicSize)[] DetermineAllParameterInfo(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            methodDeclarationSyntax.ParameterList.Parameters.Select(parameter => DetermineParameterInfo(context, parameter)).ToArray();

        ExpressionSyntax GenerateTotalParametersSizeArgument (GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax)
        {
            var parameterInfos = DetermineAllParameterInfo(context, methodDeclarationSyntax);
            int totalNonDynamicParameterByteSize = parameterInfos.Sum(parameterInfo => parameterInfo.dynamicSize ? parameterInfo.elementByteSize : 0);

            if (parameterInfos.Any(parameter => parameter.dynamicSize))
            {
                return SyntaxFactory.ParseExpression(parameterInfos.Aggregate(
                    $"{totalNonDynamicParameterByteSize + 4}",
                    (aggregate, next) => $" + {next.elementByteSize} * {GenerateGetArrayLength(next.parameter)}"));
            }

            return SyntaxFactory.ParseExpression($"{totalNonDynamicParameterByteSize + 4}");
        }

        StatementSyntax GenerateAppendRPCCall(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
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
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.ParseExpression("0") // RPCExecutionStage.Automatic
                                        /*SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName("RPCExecutionStage"),
                                            SyntaxFactory.IdentifierName("Automatic"))*/),
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
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.ParseExpression("0") // RPCExecutionStage.Automatic
                                        /*SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName("RPCExecutionStage"),
                                            SyntaxFactory.IdentifierName("Automatic"))*/),
                                            SyntaxFactory.Argument(GenerateTotalParametersSizeArgument(context, methodDeclarationSyntax)),
                                }))));


        BlockSyntax GenerateCaptureExecutionIfStatementBlock(GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.Block(SyntaxFactory.List(new StatementSyntax[]
            {
                GenerateAppendRPCCall(context, methodDeclarationSyntax),
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

        IfStatementSyntax GenerateCaptureExecutionIfStatement (GeneratorExecutionContext context, MethodDeclarationSyntax methodDeclarationSyntax) =>
            SyntaxFactory.IfStatement(
                        SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("RPCBufferIO"), SyntaxFactory.IdentifierName("CaptureExecution")),
                        GenerateCaptureExecutionIfStatementBlock(context, methodDeclarationSyntax));

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

            var methods = syntaxReceiver.methods;

            if (methods.Length == 0)
            {
                return;
            }
            MethodRewriter methodRewriter = new MethodRewriter();

            for (int mi = 0; mi < methods.Length; mi++)
            {
                try
                {
                    var type = methods[mi].Parent as TypeDeclarationSyntax;

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
                                methods[mi].ReturnType,
                                explicitInterfaceSpecifier: null,
                                identifier: SyntaxFactory.ParseToken($"Emit{methods[mi].Identifier}"),
                                typeParameterList: methods[mi].TypeParameterList != null ? methods[mi].TypeParameterList : null,
                                parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList<ParameterSyntax>(nodes: new[] {
                                    SyntaxFactory.Parameter(
                                        attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                                        modifiers: SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword)),
                                        type: SyntaxFactory.ParseTypeName(type.Identifier.ToString()),
                                        identifier: SyntaxFactory.ParseToken("instance"),
                                        @default: null)
                                })).AddParameters(methods[mi].ParameterList.Parameters.ToArray()),
                                constraintClauses: methods[mi].ConstraintClauses,
                                body: SyntaxFactory.Block(SyntaxFactory.List<StatementSyntax>(new StatementSyntax[]
                                {
                                    GenerateCaptureExecutionIfStatement(context, methods[mi]),
                                    methods[mi].ReturnType.IsKind(SyntaxKind.PredefinedType) &&
                                    ((methods[mi].ReturnType as PredefinedTypeSyntax).Keyword.ToString() == "void") ?
                                        SyntaxFactory.ExpressionStatement(GenerateInstanceMethodInvocation(context, methods[mi])) :
                                        SyntaxFactory.ReturnStatement(GenerateInstanceMethodInvocation(context, methods[mi])) as StatementSyntax
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
