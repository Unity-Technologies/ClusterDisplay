using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [InitializeOnLoad]
    public static class WrapperGenerator
    {
        static WrapperGenerator ()
        {
            RPCRegistry.generateWrapperForMethod -= TryCreateWrapper;
            RPCRegistry.generateWrapperForMethod += TryCreateWrapper;

            RPCRegistry.generateWrapperForProperty -= TryCreateWrapper;
            RPCRegistry.generateWrapperForProperty += TryCreateWrapper;
        }

        private static MemberAccessExpressionSyntax GetInstanceAccessExpression (MethodInfo methodInfo) => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("instance"), SyntaxFactory.IdentifierName(methodInfo.Name));
        private static MemberAccessExpressionSyntax GetInstanceAccessExpression (PropertyInfo propertyInfo) => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("instance"), SyntaxFactory.IdentifierName(propertyInfo.Name));

        private static AttributeSyntax GetClusterRPCAttributeSyntax () => SyntaxFactory.Attribute(SyntaxFactory.ParseName(typeof(ClusterRPC).Name));

        private static PropertyDeclarationSyntax NewProperty (PropertyInfo propertyInfo)
        {
            var instancePropertyAccessExpression = GetInstanceAccessExpression(propertyInfo);
            var propertyAssignmentExpression = SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, instancePropertyAccessExpression, SyntaxFactory.IdentifierName("value"));

            var clusterRPCAttribute = GetClusterRPCAttributeSyntax();

            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyInfo.PropertyType.FullName), propertyInfo.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    // => Getter.
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(instancePropertyAccessExpression)) 
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    // => Setter.
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        // Add [ClusterRPC] attribute.
                        .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(clusterRPCAttribute)))
                        .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(propertyAssignmentExpression))
                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        private static MethodDeclarationSyntax NewMethod (MethodInfo methodInfo)
        {
            bool hasReturnType = methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void);
            var parameters = methodInfo.GetParameters();

            var clusterRPCAttribute = GetClusterRPCAttributeSyntax();

            var separatedArgumentList = SyntaxFactory.SeparatedList<ArgumentSyntax>();
            var parametersList = SyntaxFactory.SeparatedList<ParameterSyntax>(parameters.Select(parameter =>
                {
                    var invocationArgument = SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameter.Name));
                    separatedArgumentList = separatedArgumentList.Add(invocationArgument);

                    return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name))
                        .WithType(SyntaxFactory.ParseTypeName(parameter.ParameterType.FullName));
                }).ToArray());

            var instanceMethodAccessExpression = GetInstanceAccessExpression(methodInfo);
            var invocationExpression = SyntaxFactory.InvocationExpression(instanceMethodAccessExpression, SyntaxFactory.ArgumentList(separatedArgumentList));

            return 
                SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(hasReturnType ? methodInfo.ReturnType.FullName : "void"), methodInfo.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    // Add [ClusterRPC] attribute.
                    .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(clusterRPCAttribute)))
                    .WithParameterList(SyntaxFactory.ParameterList(parametersList))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(invocationExpression))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        private static bool TryGetExistingCompilationUnit (string folderPath, string filePath, out CompilationUnitSyntax compilationUnit)
        {
            Microsoft.CodeAnalysis.SyntaxTree syntaxTree = null;
            compilationUnit = null;
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    return false;
                }

                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    var text = File.ReadAllText(filePath);
                    syntaxTree = CSharpSyntaxTree.ParseText(text);
                }
            }

            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

            return syntaxTree != null && (compilationUnit = syntaxTree.GetCompilationUnitRoot()) != null;
        }

        private static bool TryGetPropertyViaMethod (MethodInfo methodInfo, out PropertyInfo propertyInfo, out bool methodIsSetter)
        {
            propertyInfo = null;
            methodIsSetter = false;

            if (!methodInfo.IsSpecialName)
                return false;

            bool isSetter = false;
            propertyInfo = methodInfo.DeclaringType
                .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .FirstOrDefault(declaringTypeProperty =>
                {
                    if (declaringTypeProperty.GetMethod == methodInfo)
                    {
                        isSetter = false;
                        return true;
                    }

                    else if (declaringTypeProperty.SetMethod != methodInfo)
                    {
                        isSetter = true;
                        return true;
                    }

                    return false;
                });

            methodIsSetter = isSetter;
            return propertyInfo != null;
        }

        private static bool PollMethodWrappability (MethodInfo methodInfo) => methodInfo.DeclaringType.IsSubclassOf(typeof(Component));

        private static bool TryWriteCompilationUnit (string filePath, CompilationUnitSyntax compilationUnit)
        {
            try
            {
                var formattedCode = Formatter.Format(compilationUnit, new AdhocWorkspace());
                File.WriteAllText(filePath, formattedCode.ToFullString());
                File.SetAttributes(filePath, FileAttributes.ReadOnly);
            }

            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

            return true;
        }

        private static void ExtractWrapperClassDeclaration (ref CompilationUnitSyntax compilationUnit, out NamespaceDeclarationSyntax nsDecl, out ClassDeclarationSyntax classDecl)
        {
            var compilationUnitMembers = compilationUnit.Members;

            nsDecl = compilationUnitMembers.FirstOrDefault() as NamespaceDeclarationSyntax;
            compilationUnitMembers = compilationUnitMembers.Remove(nsDecl);
            compilationUnit = compilationUnit.WithMembers(compilationUnitMembers);

            var nsMembers = nsDecl.Members;

            classDecl = nsMembers.FirstOrDefault() as ClassDeclarationSyntax;
            nsMembers = nsMembers.Remove(classDecl);
            nsDecl = nsDecl.WithMembers(nsMembers);
        }

        private static bool TryGetExistingMethodDeclaration (MethodInfo methodInfo, ClassDeclarationSyntax classDecl, out MethodDeclarationSyntax outMethodDecl)
        {
            return (outMethodDecl = (classDecl.Members
                .FirstOrDefault(memberDecl =>
                {
                    if (!(memberDecl is MethodDeclarationSyntax))
                        return false;

                    var methodDecl = memberDecl as MethodDeclarationSyntax;

                    return
                        methodDecl.Identifier.Text == methodInfo.Name &&
                        methodDecl.ReturnType.ToString() == methodInfo.ReturnType.Name &&
                        methodDecl.ParameterList.Parameters.All(parameterSyntax => methodInfo.GetParameters().Any(parameter => parameter.ParameterType.Name == parameterSyntax.Type.ToString()));

                })) as MethodDeclarationSyntax) != null;
        }

        private static bool TryGetExistingPropertyDeclaration (PropertyInfo propertyInfo, ClassDeclarationSyntax classDecl, out PropertyDeclarationSyntax outPropertyDecl)
        {
            return (outPropertyDecl = (classDecl.Members
                .FirstOrDefault(memberDecl =>
                {
                    if (!(memberDecl is PropertyDeclarationSyntax))
                        return false;

                    var propertyDecl = memberDecl as PropertyDeclarationSyntax;

                    return
                        propertyDecl.Identifier.Text == propertyInfo.Name &&
                        propertyDecl.Type.ToString() == propertyInfo.PropertyType.Name;

                })) as PropertyDeclarationSyntax) != null;
        }

        private static ClassDeclarationSyntax NewWrapperClass (System.Type wrappedType, string wrapperName)
        {
            var componentWrapperType = typeof(ComponentWrapper<>);
            var genericArguments = componentWrapperType.GetGenericArguments();
            var genericTypeCountStr = $"`{genericArguments.Count()}";
            var wrapperTypeName = $"{componentWrapperType.Name.Replace(genericTypeCountStr, "")}<{wrappedType.Name}>";

            var requireComponentAttributeArgumentExpression = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({wrappedType.Name})"));

            return SyntaxFactory.ClassDeclaration(SyntaxFactory.ParseToken(wrapperName))
                .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory.ParseName(typeof(RequireComponent).Name),
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(requireComponentAttributeArgumentExpression))))))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(wrapperTypeName, consumeFullText: true)));
        }

        private static NamespaceDeclarationSyntax NewNamespace () => SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(WrapperUtils.WrapperNamespace));

        private static CompilationUnitSyntax CreateNewCompilationUnit (System.Type wrappedType, string wrapperName)
        {
             var compilationUnit = SyntaxFactory.CompilationUnit();

            SyntaxList<UsingDirectiveSyntax> usingDirectives = new SyntaxList<UsingDirectiveSyntax>();
            if (wrappedType.Namespace != null)
                usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(wrappedType.Namespace)));
            usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityEngine")));

            for (int ui = 0; ui < usingDirectives.Count; ui++)
                compilationUnit = compilationUnit.AddUsings(usingDirectives[ui]);

            return compilationUnit;
        }

        public static bool TryCreateWrapper (PropertyInfo propertyInfo)
        {
            if (!PollMethodWrappability(propertyInfo.SetMethod))
                return false;

            WrapperUtils.GetCompilationUnitPath(propertyInfo.SetMethod.DeclaringType, out var wrapperName, out var folderPath, out var filePath);
            if (TryGetExistingCompilationUnit(folderPath, filePath, out var compilationUnit))
            {
                ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);
                if (!TryGetExistingPropertyDeclaration(propertyInfo, classDecl, out var propertyDecl))
                    compilationUnit = compilationUnit
                        .AddMembers(nsDecl
                            .AddMembers(classDecl
                                .AddMembers(NewProperty(propertyInfo))));
            }

            else compilationUnit = CreateNewCompilationUnit(propertyInfo.DeclaringType, wrapperName)
                    .AddMembers(
                        NewNamespace()
                            .AddMembers(
                                NewWrapperClass(propertyInfo.DeclaringType, wrapperName)
                                    .AddMembers(NewProperty(propertyInfo))));

            return TryWriteCompilationUnit(filePath, compilationUnit);
        }

        public static bool TryCreateWrapper (MethodInfo methodInfo)
        {
            if (!PollMethodWrappability(methodInfo))
                return false;

            WrapperUtils.GetCompilationUnitPath(methodInfo.DeclaringType, out var wrapperName, out var folderPath, out var filePath);
            if (TryGetExistingCompilationUnit(folderPath, filePath, out var compilationUnit))
            {
                ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);

                if (!TryGetExistingMethodDeclaration(methodInfo, classDecl, out var methodDecl))
                    compilationUnit = compilationUnit
                        .AddMembers(nsDecl
                            .AddMembers(classDecl
                                .AddMembers(NewMethod(methodInfo))));
            }

            else compilationUnit = CreateNewCompilationUnit(methodInfo.DeclaringType, wrapperName)
                    .AddMembers(
                        NewNamespace()
                            .AddMembers(
                                NewWrapperClass(methodInfo.DeclaringType, wrapperName)
                                    .AddMembers(NewMethod(methodInfo))));

            return TryWriteCompilationUnit(filePath, compilationUnit);
        }
    }
}
