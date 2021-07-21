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

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    [InitializeOnLoad]
    public static class WrapperGenerator
    {
        static WrapperGenerator ()
        {
            RPCRegistry.onAddWrappableMethod -= TryWrapMethod;
            RPCRegistry.onAddWrappableMethod += TryWrapMethod;

            RPCRegistry.onAddWrappableProperty -= TryWrapProperty;
            RPCRegistry.onAddWrappableProperty += TryWrapProperty;

            RPCRegistry.onRemoveMethodWrapper -= TryRemoveWrapper;
            RPCRegistry.onRemoveMethodWrapper += TryRemoveWrapper;

            RPCRegistry.onRemovePropertyWrapper -= TryRemoveWrapper;
            RPCRegistry.onRemovePropertyWrapper += TryRemoveWrapper;
        }

        private static MemberAccessExpressionSyntax NewInstanceAccessExpression (MethodInfo methodInfo) => 
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("instance"), SyntaxFactory.IdentifierName(methodInfo.Name));
        private static MemberAccessExpressionSyntax NewInstanceAccessExpression (PropertyInfo propertyInfo) => 
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("instance"), SyntaxFactory.IdentifierName(propertyInfo.Name));

        private static AttributeSyntax NewRPCAttribute () => 
            SyntaxFactory.Attribute(SyntaxFactory.ParseName(typeof(ClusterRPC).Name));

        private static bool MethodSignaturesAreEqual (MethodDeclarationSyntax methodDecl, MethodInfo methodInfo)
        {
            var parameterSyntaxList = methodDecl.ParameterList.Parameters;
            var parameters = methodInfo.GetParameters();

            if (methodDecl.ReturnType.ToString() != methodInfo.ReturnType.FullName ||
                methodDecl.Identifier.Text != methodInfo.Name ||
                parameterSyntaxList.Count != parameters.Length)
                return false;

            for (int pi = 0; pi < parameters.Length; pi++)
                if (parameterSyntaxList[pi].Type.ToString() != parameters[pi].ParameterType.FullName ||
                    parameterSyntaxList[pi].Identifier.Text != parameters[pi].Name)
                    return false;
            return true;
        }

        private static bool PropertySignaturesAreEqual (PropertyDeclarationSyntax propertyDecl, PropertyInfo propertyInfo) => 
                propertyDecl.Type.ToString() == propertyInfo.PropertyType.FullName &&
                propertyDecl.Identifier.Text == propertyInfo.Name;

        private static PrefixUnaryExpressionSyntax NewTryGetInstanceExpression (MemberInfo memberInfo) => 
            SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, 
                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("TryGetInstance"),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                            SyntaxFactory.Argument(SyntaxFactory.ParseExpression($"{memberInfo.DeclaringType.FullName} instance"))
                            .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword))))));

        private static PropertyDeclarationSyntax NewProperty (PropertyInfo propertyInfo)
        {
            var clusterRPCAttribute = NewRPCAttribute();

            var tryGetInstanceExpression = NewTryGetInstanceExpression(propertyInfo);
            var instancePropertyAccessExpression = NewInstanceAccessExpression(propertyInfo);

            var getterReturnStatement = SyntaxFactory.ReturnStatement(SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(propertyInfo.PropertyType.FullName)));
            var setterReturnStatement = SyntaxFactory.ReturnStatement();

            var getterIfStatement = SyntaxFactory.IfStatement(tryGetInstanceExpression, getterReturnStatement);
            var setterIfStatement = SyntaxFactory.IfStatement(tryGetInstanceExpression, setterReturnStatement);

            var returnStatement = SyntaxFactory.ReturnStatement(instancePropertyAccessExpression);
            var propertyAssignmentExpression = SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, instancePropertyAccessExpression, SyntaxFactory.IdentifierName("value")));

            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyInfo.PropertyType.FullName), propertyInfo.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    // => Getter.
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithBody(SyntaxFactory.Block(getterIfStatement, returnStatement)),
                    // => Setter.
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        // Add [ClusterRPC] attribute.
                        .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(clusterRPCAttribute)))
                        .WithBody(SyntaxFactory.Block(setterIfStatement, propertyAssignmentExpression)));
        }

        private static MethodDeclarationSyntax NewMethod (MethodInfo methodInfo)
        {
            bool hasReturnType = methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void);
            var parameters = methodInfo.GetParameters();

            var clusterRPCAttribute = NewRPCAttribute();

            var tryGetInstanceExpression = NewTryGetInstanceExpression(methodInfo);
            var instancePropertyAccessExpression = NewInstanceAccessExpression(methodInfo);
            var tryGetFailureReturnStatement = hasReturnType ?
                SyntaxFactory.ReturnStatement(SyntaxFactory.DefaultExpression(SyntaxFactory.ParseTypeName(methodInfo.ReturnType.FullName))) :
                SyntaxFactory.ReturnStatement();

            var tryGetIfStatement = SyntaxFactory.IfStatement(tryGetInstanceExpression, tryGetFailureReturnStatement);

            var separatedArgumentList = SyntaxFactory.SeparatedList<ArgumentSyntax>();
            var parametersList = SyntaxFactory.SeparatedList<ParameterSyntax>(parameters.Select(parameter =>
                {
                    var invocationArgument = SyntaxFactory.Argument(SyntaxFactory.ParseExpression(parameter.Name));
                    separatedArgumentList = separatedArgumentList.Add(invocationArgument);

                    return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name))
                        .WithType(SyntaxFactory.ParseTypeName(parameter.ParameterType.FullName));
                }).ToArray());

            var instanceMethodAccessExpression = NewInstanceAccessExpression(methodInfo);
            var invocationExpression = SyntaxFactory.InvocationExpression(instanceMethodAccessExpression, SyntaxFactory.ArgumentList(separatedArgumentList));
            var emptyOrReturnStatement = hasReturnType ?
                SyntaxFactory.ReturnStatement(invocationExpression) :
                SyntaxFactory.ExpressionStatement(invocationExpression) as StatementSyntax;

            return
                SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(hasReturnType ? methodInfo.ReturnType.FullName : "void"), methodInfo.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    // Add [ClusterRPC] attribute.
                    .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(clusterRPCAttribute)))
                    .WithParameterList(SyntaxFactory.ParameterList(parametersList))
                    .WithBody(SyntaxFactory.Block(tryGetIfStatement, emptyOrReturnStatement));
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

            return
                SyntaxFactory.ClassDeclaration(SyntaxFactory.ParseToken(wrapperName))
                    .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Attribute(
                            SyntaxFactory.ParseName(typeof(RequireComponent).Name),
                            SyntaxFactory.AttributeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(requireComponentAttributeArgumentExpression))))))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(wrapperTypeName, consumeFullText: true)));
        }

        private static NamespaceDeclarationSyntax NewNamespace () => SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(WrapperUtils.WrapperNamespace));

        private static CompilationUnitSyntax CreateNewCompilationUnit (System.Type wrappedType, MemberDeclarationSyntax wrappingMember, string wrapperName)
        {
             var compilationUnit = SyntaxFactory.CompilationUnit();

            SyntaxList<UsingDirectiveSyntax> usingDirectives = new SyntaxList<UsingDirectiveSyntax>();
            if (wrappedType.Namespace != null)
                usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(wrappedType.Namespace)));
            usingDirectives = usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityEngine")));

            for (int ui = 0; ui < usingDirectives.Count; ui++)
                compilationUnit = compilationUnit.AddUsings(usingDirectives[ui]);

            return compilationUnit
                    .AddMembers(
                        NewNamespace()
                            .AddMembers(
                                NewWrapperClass(wrappedType, wrapperName)
                                    .AddMembers(wrappingMember)));
        }

        private static CompilationUnitSyntax UpdateWrapperClass (
            CompilationUnitSyntax compilationUnit, 
            NamespaceDeclarationSyntax nsDecl, 
            ClassDeclarationSyntax classDecl, 
            MemberDeclarationSyntax memberDecl) =>
                compilationUnit
                    .AddMembers(nsDecl
                        .AddMembers(classDecl
                            .AddMembers(memberDecl)));

        private static CompilationUnitSyntax UpdateWrapperClass (
            CompilationUnitSyntax compilationUnit, 
            NamespaceDeclarationSyntax nsDecl, 
            ClassDeclarationSyntax classDecl, 
            SyntaxList<MemberDeclarationSyntax> members) =>
                compilationUnit
                    .AddMembers(nsDecl
                        .AddMembers(classDecl
                            .WithMembers(members)));

        private static void PollFoldersToCleanUp (string wrapperFolderPath)
        {
            // Clean up the folders if there are no remaining wrappers.
            var remainingWrappers = Directory.GetFiles(wrapperFolderPath);
            if (remainingWrappers.Length != 0)
                return;

            Directory.Delete(wrapperFolderPath); // Delete Assets/ClusterDisplay/Wrappers/

            var wrapperFolderMetaFilePath = $"{wrapperFolderPath}.meta";
            if (File.Exists(wrapperFolderMetaFilePath))
                File.Delete(wrapperFolderMetaFilePath); // Delete Assets/ClusterDisplay/Wrappers.meta

            var clusterDisplayFolder = Path.GetDirectoryName(wrapperFolderPath);
            var otherRemainingFiles = Directory.GetFiles(clusterDisplayFolder);

            if (otherRemainingFiles.Length == 0)
            {
                Directory.Delete(clusterDisplayFolder); // Delete Assets/ClusterDisplay/

                var clusterDisplayFolderMetaFilePath = $"{clusterDisplayFolder}.meta";
                if (File.Exists(clusterDisplayFolderMetaFilePath))
                    File.Delete(clusterDisplayFolderMetaFilePath); // Delete Assets/ClusterDisplay.meta
            }
        }

        public static bool TryRemoveWrapper (PropertyInfo wrappingProperty)
        {
            WrapperUtils.GetCompilationUnitPath(
                wrappingProperty.DeclaringType, 
                typeIsWrapper: true, 
                out var wrapperName, 
                out var wrapperFolderPath, 
                out var wrapperFilePath);

            if (!SourceGeneratorUtils.TryGetExistingCompilationUnit(wrapperFilePath, out var compilationUnit))
                return true;

            ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);

            var members = classDecl.Members;
            var indexOfProperty = members.IndexOf(memberDecl =>
            {
                if (!(memberDecl is PropertyDeclarationSyntax))
                    return false;
                var propertyDecl = memberDecl as PropertyDeclarationSyntax;
                return PropertySignaturesAreEqual(propertyDecl, wrappingProperty);
            });

            if (indexOfProperty == -1)
                return true;

            members = members.RemoveAt(indexOfProperty);
            if (members.Count == 0)
            {
                SourceGeneratorUtils.RemoveCompilationUnit(wrapperFilePath, wrapperName);
                PollFoldersToCleanUp(wrapperFolderPath);
                return true;
            }

            compilationUnit = UpdateWrapperClass(compilationUnit, nsDecl, classDecl, members);
            return SourceGeneratorUtils.TryWriteCompilationUnit(wrapperFilePath, compilationUnit);
        }

        public static bool TryRemoveWrapper (MethodInfo wrappingMethod)
        {
            WrapperUtils.GetCompilationUnitPath(
                wrappingMethod.DeclaringType, 
                typeIsWrapper: true,
                out var wrapperName, 
                out var wrapperFolderPath, 
                out var wrapperFilePath);

            if (!SourceGeneratorUtils.TryGetExistingCompilationUnit(wrapperFilePath, out var compilationUnit))
                return true;

            ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);

            var members = classDecl.Members;
            var indexOfMethod = members.IndexOf(memberDecl =>
            {
                if (!(memberDecl is MethodDeclarationSyntax))
                    return false;
                var methodDecl = memberDecl as MethodDeclarationSyntax;
                return MethodSignaturesAreEqual(methodDecl, wrappingMethod);
            });

            if (indexOfMethod == -1)
                return true;

            members = members.RemoveAt(indexOfMethod);
            if (members.Count == 0)
            {
                SourceGeneratorUtils.RemoveCompilationUnit(wrapperFilePath, wrapperName);
                PollFoldersToCleanUp(wrapperFolderPath);
                return true;
            }

            compilationUnit = UpdateWrapperClass(compilationUnit, nsDecl, classDecl, members);
            return SourceGeneratorUtils.TryWriteCompilationUnit(wrapperFilePath, compilationUnit);
        }

        public static bool TryWrapProperty (PropertyInfo propertyToWrap)
        {
            if (!PollMethodWrappability(propertyToWrap.SetMethod))
                return false;

            WrapperUtils.GetCompilationUnitPath(
                propertyToWrap.SetMethod.DeclaringType,
                typeIsWrapper: false,
                out var wrapperName, 
                out var folderPath, 
                out var filePath);

            if (SourceGeneratorUtils.TryGetExistingCompilationUnit(filePath, out var compilationUnit))
            {
                ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);
                if (!TryGetExistingPropertyDeclaration(propertyToWrap, classDecl, out var propertyDecl))
                    compilationUnit = compilationUnit
                        .AddMembers(nsDecl
                            .AddMembers(classDecl
                                .AddMembers(NewProperty(propertyToWrap))));
            }

            else compilationUnit = CreateNewCompilationUnit(propertyToWrap.DeclaringType, NewProperty(propertyToWrap), wrapperName);

            return SourceGeneratorUtils.TryWriteCompilationUnit(filePath, compilationUnit);
        }

        public static bool TryWrapMethod (MethodInfo methodToWrap)
        {
            if (!PollMethodWrappability(methodToWrap))
                return false;

            WrapperUtils.GetCompilationUnitPath(
                methodToWrap.DeclaringType, 
                typeIsWrapper: false,
                out var wrapperName, 
                out var folderPath, 
                out var filePath);

            if (SourceGeneratorUtils.TryGetExistingCompilationUnit(filePath, out var compilationUnit))
            {
                ExtractWrapperClassDeclaration(ref compilationUnit, out var nsDecl, out var classDecl);
                if (!TryGetExistingMethodDeclaration(methodToWrap, classDecl, out var methodDecl))
                    compilationUnit = UpdateWrapperClass(compilationUnit, nsDecl, classDecl, methodDecl);
            }

            else compilationUnit = CreateNewCompilationUnit(methodToWrap.DeclaringType, NewMethod(methodToWrap), wrapperName);

            return SourceGeneratorUtils.TryWriteCompilationUnit(filePath, compilationUnit);
        }
    }
}
