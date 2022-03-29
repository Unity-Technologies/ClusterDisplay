using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public abstract class BuiltInInspectorExtension : ClusterDisplayInspectorExtension
    {
        protected static Type[] cachedTypesWithCustomEditorAttribute = null;
        protected static Type[] cachedTypesWithCustomEditorForRenderPipelineAttribute = null;

        protected readonly Dictionary<string, MethodInfo> cachedReflectionAccessibleMethods = new Dictionary<string, MethodInfo>();

        protected Type cachedEditorType;
        protected Type cachedAssignedType;
        protected Type cachedDefaultCustomEditorType;

        private const string OnHeaderGUIMethodString = "OnHeaderGUI";
        private const string OnSceneGUIMethodString = "OnSceneGUI";
        private const string ShouldHideOpenButtonMethodString = "ShouldHideOpenButton";

        private static readonly string[] TargetMethodStrings = new string[3]
        {
            OnHeaderGUIMethodString,
            ShouldHideOpenButtonMethodString,
            OnSceneGUIMethodString,
        };

        private UnityEditor.Editor cachedDefaultEditorInstance;
        protected UnityEditor.Editor DefaultEditorInstance
        {
            get
            {
                if (cachedDefaultEditorInstance == null)
                {
                    cachedDefaultEditorInstance = UnityEditor.Editor.CreateEditor(targets, cachedDefaultCustomEditorType);
                    if (cachedDefaultEditorInstance == null)
                        CodeGenDebug.LogError($"Unable to create custom editor instance of type: \"{cachedDefaultCustomEditorType.FullName}\" for instance of: \"{cachedAssignedType.FullName}\".");
                }

                return cachedDefaultEditorInstance;
            }
        }

        static BuiltInInspectorExtension ()
        {
            if (cachedTypesWithCustomEditorAttribute == null)
                cachedTypesWithCustomEditorAttribute = UnityEditor.TypeCache.GetTypesWithAttribute<CustomEditor>()
                    .Where(type =>
                    {
                        if (type == null)
                            return false;

                        // We don't want any generated custom editors.
                        if (type.Namespace == GeneratedInspectorNamespace)
                            return false;

                        var customEditorAttributes = type.GetCustomAttributes<CustomEditor>();
                        return customEditorAttributes.Count() > 0;
                    })
                    .ToArray();

            if (cachedTypesWithCustomEditorForRenderPipelineAttribute == null)
                cachedTypesWithCustomEditorForRenderPipelineAttribute = UnityEditor.TypeCache.GetTypesWithAttribute<CustomEditorForRenderPipelineAttribute>()
                        .Where(type =>
                        {
                            if (type == null)
                                return false;

                            // We don't want any generated custom editors.
                            if (type.Namespace == GeneratedInspectorNamespace)
                                return false;

                            return type.GetCustomAttributes<CustomEditorForRenderPipelineAttribute>().Count() > 0;
                        })
                        .ToArray();
        }

        public BuiltInInspectorExtension(bool useDefaultInspector) : base(useDefaultInspector) => Initialize();

        private bool TryGetAssignedCustomEditorType (Type type, out Type assignedType, bool throwError = true)
        {
            // I've stepped into this method, and it seems that even when customAttributeData.AttributeType is typeof(CustomEditor), determining
            // whether it's that type still returns false, so for now were comparing the names.
            var customEditorFullName = typeof(CustomEditor).FullName;
            var customEditorForRenderPipelineAttributeFullName = typeof(CustomEditorForRenderPipelineAttribute).FullName;

            foreach (var customAttributeData in type.GetCustomAttributesData())
            {
                if (customAttributeData.AttributeType.FullName != customEditorFullName && customAttributeData.AttributeType.FullName != customEditorForRenderPipelineAttributeFullName)
                    continue;

                foreach (var constructorArgument in customAttributeData.ConstructorArguments)
                {
                    if (constructorArgument.ArgumentType != typeof(Type))
                        continue;

                    assignedType = constructorArgument.Value as Type;
                    return true;
                }
            }

            if (throwError)
                CodeGenDebug.LogError($"Unable to find assigned type in {nameof(CustomEditor)} attribute constructor for type: \"{type.FullName}\".");

            assignedType = null;
            return false;
        }

        private bool TypeHasCustomEditorWithMatchingType (System.Type type) => 
            TryGetAssignedCustomEditorType(type, out var assignedType, throwError: false) && 
            assignedType == cachedAssignedType;

        private bool TryFindCustomEditorType(out System.Type customEditorType)
        {
            if ((customEditorType = cachedTypesWithCustomEditorForRenderPipelineAttribute.FirstOrDefault(TypeHasCustomEditorWithMatchingType)) == null)
                return (customEditorType = cachedTypesWithCustomEditorAttribute.FirstOrDefault(TypeHasCustomEditorWithMatchingType)) != null;
            return true;
        }

        private void Initialize ()
        {
            if (cachedEditorType == null)
                cachedEditorType = this.GetType();

            if (cachedAssignedType == null)
                if (!TryGetAssignedCustomEditorType(cachedEditorType, out cachedAssignedType))
                    return;

            if (cachedDefaultCustomEditorType == null)
                if (!TryFindCustomEditorType(out cachedDefaultCustomEditorType))
                    return;


            if (cachedReflectionAccessibleMethods.Count == 0)
            {
                for (int i = 0; i < TargetMethodStrings.Length; i++)
                {
                    var method = cachedDefaultCustomEditorType.GetMethod(TargetMethodStrings[i], BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null)
                        continue;

                    if (!cachedReflectionAccessibleMethods.ContainsKey(TargetMethodStrings[i]))
                        cachedReflectionAccessibleMethods.Add(TargetMethodStrings[i], method);
                }
            }
        }

        private void TryCallCachedMethod (string methodName, bool throwError = true)
        {
            if (cachedDefaultCustomEditorType == null)
                goto failure;

            if (!cachedReflectionAccessibleMethods.TryGetValue(methodName, out var method))
                goto failure;

            if (method == null)
                goto failure;

            method.Invoke(cachedDefaultEditorInstance, null);

            failure:
            if (throwError)
                CodeGenDebug.LogError($"Unable to invoke method: \"{methodName}\" in custom editor: \"{cachedDefaultCustomEditorType.FullName}\", it does not exist!");
        }

        private ReturnType TryCallCachedMethod<ReturnType> (string methodName, bool throwError = true)
            where ReturnType : struct
        {
            if (!cachedReflectionAccessibleMethods.TryGetValue(methodName, out var method))
                goto failure;

            if (method == null)
                goto failure;

            return (ReturnType)method.Invoke(cachedDefaultEditorInstance, null);

            failure:
            if (throwError)
                CodeGenDebug.LogError($"Unable to invoke method: \"{methodName}\" in custom editor: \"{cachedDefaultCustomEditorType.FullName}\", it does not exist!");
            return default(ReturnType);
        }

        public void OnSceneGUI() => TryCallCachedMethod(OnSceneGUIMethodString, throwError: false);
        protected override void OnHeaderGUI() => TryCallCachedMethod(OnHeaderGUIMethodString, throwError: false);
        protected override bool ShouldHideOpenButton() => TryCallCachedMethod<bool>(ShouldHideOpenButtonMethodString, throwError: false);
        public override VisualElement CreateInspectorGUI() => DefaultEditorInstance.CreateInspectorGUI();
        public override void DrawPreview(Rect previewArea) => DefaultEditorInstance.DrawPreview(previewArea);
        public override string GetInfoString() => DefaultEditorInstance.GetInfoString();
        public override GUIContent GetPreviewTitle() => DefaultEditorInstance.GetPreviewTitle();
        public override bool HasPreviewGUI() => DefaultEditorInstance.HasPreviewGUI();
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background) => DefaultEditorInstance.OnInteractivePreviewGUI(r, background);
        public override void OnPreviewGUI(Rect r, GUIStyle background) => DefaultEditorInstance.OnPreviewGUI(r, background);
        public override void OnPreviewSettings() => DefaultEditorInstance.OnPreviewSettings();
        public override void ReloadPreviewInstances() => DefaultEditorInstance.ReloadPreviewInstances();
        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height) => DefaultEditorInstance.RenderStaticPreview(assetPath, subAssets, width, height);
        public override bool RequiresConstantRepaint() => DefaultEditorInstance.RequiresConstantRepaint();
        public override bool UseDefaultMargins() => DefaultEditorInstance.UseDefaultMargins();
    }
}
