using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

using Unity.ClusterDisplay.Editor.Inspectors;
using System.Collections.Generic;
using System.IO;

#if CLUSTER_DISPLAY_URP
using UnityEngine.Rendering.Universal;
#elif CLUSTER_DISPLAY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace Unity.ClusterDisplay.Editor.SourceGenerators
{
    [InitializeOnLoad]
    public static class InspectorGenerator
    {
        /*
        private struct AssemblyDefinitionBasis
        {
            private string name;
            private string[] references;
            private string[] includePlatforms;

            public AssemblyDefinitionBasis (string[] referencedAssemblyGUIDs)
            {
                name = "Unity.ClusterDisplay.Editor.Inspectors.Generated";
                references = referencedAssemblyGUIDs;
                includePlatforms = new string[] { "Editor" };
            }
        }
        */

        private static string GetTypeNameWithoutGenericArugmentCount (System.Type genericType, out int genericArgumentCount)
        {
            genericArgumentCount = genericType.GetGenericArguments().Count();
            var genericTypeCountStr = $"`{genericArgumentCount}";
            return genericType.FullName.Replace(genericTypeCountStr, "");
        }

        private static string GetGeneratedInspectorTypeName (System.Type targetType, System.Type customEditorAttributeType) =>
            customEditorAttributeType == typeof(CustomEditor) ? $"{targetType.Name}Extension" : $"{targetType.Name}RenderPipelineExtension";

        private static bool TryGetCustomEditorTargetType (
            System.Type customEditorType, 
            out System.Type targetType, 
            out System.Type customEditorAttributeType, 
            out System.Type renderPipeline) // If the attribute is a CustomEditorForRenderPipeline, otherwise this is null.
        {
            targetType = null;
            customEditorAttributeType = null;
            renderPipeline = null;

            CustomEditor customEditorAttribute = customEditorType.GetCustomAttributes<CustomEditorForRenderPipelineAttribute>().FirstOrDefault();
            var isCustomEditorForRenderPipeline = false;

            if (customEditorAttribute == null)
                customEditorAttribute = customEditorType.GetCustomAttributes<CustomEditor>().FirstOrDefault();
            else isCustomEditorForRenderPipeline = true;

            if (customEditorAttribute == null)
                return false;

            var attributeType = customEditorAttributeType = customEditorAttribute.GetType();
            var customAttributeData = customEditorType.GetCustomAttributesData().FirstOrDefault(customAttributesData => customAttributesData != null && customAttributesData.AttributeType == attributeType);

            if (customAttributeData == null)
                return false;

            var constructorArguments = customAttributeData.ConstructorArguments.ToArray();
            if (constructorArguments.Length == 0)
                return false;

            var typeArgument = constructorArguments[0].Value;
            if (isCustomEditorForRenderPipeline)
            {
                if (constructorArguments.Length < 2)
                    return false;
                renderPipeline = constructorArguments[1].Value as System.Type;
            }

            targetType = targetType = typeArgument as Type;
            if (targetType == null)
                return false;

            return true;
        }

        private static ClassDeclarationSyntax GenerateInspectorClass (string className, System.Type targetType)
        {
            var baseType = typeof(UnityWrapperInspectorExtension<,>);
            string baseTypeName = GetTypeNameWithoutGenericArugmentCount(baseType, out var _);
            var wrapperTypeName = $"{baseTypeName}<{targetType.FullName}, {GetTypeNameWithoutGenericArugmentCount(typeof(ComponentWrapper<>), out var _)}<{targetType.FullName}>>";
            return
                SyntaxFactory.ClassDeclaration(className)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(wrapperTypeName)));
        }

        [MenuItem("Unity/Cluster Display/Generate Inspectors")]
        public static void Generate ()
        {
            var customEditorTypes = ReflectionUtils.GetAllTypes().Where(type =>
            {
                return
                    (type.GetCustomAttributes<CustomEditor>().Count() > 0 ||
                    type.GetCustomAttributes<CustomEditorForRenderPipelineAttribute>().Count() > 0);
            }).Distinct();

            var asmdefFilePath = AssetDatabase.FindAssets(BuiltInInspectorExtension.GeneratedInspectorNamespace).Select(guid => AssetDatabase.GUIDToAssetPath(guid)).FirstOrDefault(assetPath => Path.GetExtension(assetPath) == ".asmdef");
            if (asmdefFilePath == null)
            {
                Debug.LogError($"Unable to find assembly definition for generated inspectors under name: \"{BuiltInInspectorExtension.GeneratedInspectorNamespace}\".");
                return;
            }

            var generatedFolderPath = Path.GetDirectoryName(asmdefFilePath);
            var generatedFilePath = $"{generatedFolderPath}/GeneratedInspectors.cs";

            NamespaceDeclarationSyntax nsDecl = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(ClusterDisplayInspectorExtension.GeneratedInspectorNamespace));

            SyntaxList<MemberDeclarationSyntax> nsMembers = new SyntaxList<MemberDeclarationSyntax>();

            HashSet<string> generatedInspectorClassNames = new HashSet<string>();
            List<string> assemblyDefinitionGUID = new List<string>();

            foreach (var customEditorType in customEditorTypes)
            {
                if (!TryGetCustomEditorTargetType(
                    customEditorType, 
                    out var targetType, 
                    out var customEditorAttributeType, 
                    out var renderPipeline))
                    continue;

                if (!targetType.IsSubclassOf(typeof(Component)))
                    continue;

                var className = GetGeneratedInspectorTypeName(targetType, customEditorAttributeType);
                if (generatedInspectorClassNames.Contains(className))
                    continue;

                var generatedInspectorClass = GenerateInspectorClass(className, targetType);

                if (renderPipeline != null)
                {
                    var firstGenericArgument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({targetType.FullName})"));
                    #if CLUSTER_DISPLAY_URP
                    var secondGenericArgument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({typeof(UniversalRenderPipelineAsset).FullName})"));
                    #elif CLUSTER_DISPLAY_HDRP
                    var secondGenericArgument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({typeof(HDRenderPipelineAsset).FullName})"));
                    #endif

                    generatedInspectorClass = generatedInspectorClass.AddAttributeLists(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SeparatedList(
                                new SyntaxList<AttributeSyntax>(
                                    SyntaxFactory.Attribute(
                                        SyntaxFactory.ParseName(customEditorAttributeType.FullName))
                                    .AddArgumentListArguments(firstGenericArgument, secondGenericArgument)))));
                }

                else
                {
                    var customEditorAttributeArgumentExpression = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"typeof({targetType.FullName})"));
                    generatedInspectorClass = generatedInspectorClass.AddAttributeLists(
                        SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Attribute(SyntaxFactory.ParseName(customEditorAttributeType.FullName))
                            .AddArgumentListArguments(customEditorAttributeArgumentExpression))));
                }

                generatedInspectorClassNames.Add(className);
                nsMembers = nsMembers.Add(generatedInspectorClass);
            }

            var compilationUnit = SyntaxFactory.CompilationUnit()
                .AddMembers(nsDecl.WithMembers(nsMembers));

            SourceGeneratorUtils.TryWriteCompilationUnit(generatedFilePath, compilationUnit);
            AssetDatabase.Refresh();
        }
    }
}
