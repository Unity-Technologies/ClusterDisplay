using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public abstract class UnityInspectorExtension : InspectorExtension
    {
        protected static Type[] cachedTypesWithCustomEditorAttribute = null;

        public UnityInspectorExtension(bool useDefaultInspector) : base(useDefaultInspector)
        {
            if (cachedTypesWithCustomEditorAttribute == null)
                cachedTypesWithCustomEditorAttribute = ReflectionUtils.GetAllTypes().Where(type => type != null && type.GetCustomAttributes<CustomEditor>().Count() > 0).ToArray();
        }
    }

    public abstract class UnityInspectorExtension<InstanceType> : UnityInspectorExtension, IInspectorExtension<InstanceType>
        where InstanceType : Component
    {
        private readonly static Dictionary<string, MethodInfo> cachedMethods = new Dictionary<string, MethodInfo>();

        protected static Type cachedEditorType;
        protected static Type cachedAssignedType;
        protected static Type cachedDefaultCustomEditorType;

        private const string OnHeaderGUIMethodString = "OnHeaderGUI";
        private const string OnSceneGUIMethodString = "OnSceneGUI";
        private const string ShouldHideOpenButtonMethodString = "ShouldHideOpenButton";

        private static readonly string[] TargetMethodStrings = new string[3]
        {
            OnHeaderGUIMethodString,
            ShouldHideOpenButtonMethodString,
            OnSceneGUIMethodString,
        };

        private UnityEditor.Editor cachedEditorInstance;

        protected UnityEditor.Editor EditorInstance
        {
            get
            {
                if (cachedEditorInstance == null)
                {
                    cachedEditorInstance = UnityEditor.Editor.CreateEditor(targets, cachedDefaultCustomEditorType);
                    if (cachedEditorInstance == null)
                        Debug.LogError($"Unable to create custom editor instance of type: \"{cachedDefaultCustomEditorType.FullName}\" for instance of: \"{cachedAssignedType.FullName}\".");
                }

                return cachedEditorInstance;
            }
        }

        public UnityInspectorExtension(bool useDefaultInspector = true) : base(useDefaultInspector) => Initialize();

        private void TryCallCachedMethod (string methodName, bool throwError = true)
        {
            if (!cachedMethods.TryGetValue(methodName, out var method))
                goto failure;

            if (method == null)
                goto failure;

            method.Invoke(cachedEditorInstance, null);

            failure:
            if (throwError)
                Debug.LogError($"Unable to invoke method: \"{methodName}\" in custom editor: \"{cachedDefaultCustomEditorType.FullName}\", it does not exist!");
        }

        private A TryCallCachedMethod<A> (string methodName, bool throwError = true) where A : struct
        {
            if (!cachedMethods.TryGetValue(methodName, out var method))
                goto failure;

            if (method == null)
                goto failure;

            return (A)method.Invoke(cachedEditorInstance, null);

            failure:
            if (throwError)
                Debug.LogError($"Unable to invoke method: \"{methodName}\" in custom editor: \"{cachedDefaultCustomEditorType.FullName}\", it does not exist!");
            return default(A);
        }

        private bool TryGetAssignedCustomEditorType (Type type, out Type assignedType, bool throwError = true)
        {
            foreach (var customAttributeData in type.GetCustomAttributesData())
            {
                if (customAttributeData.AttributeType != typeof(CustomEditor))
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
                Debug.LogError($"Unable to find assigned type in {nameof(CustomEditor)} attribute constructor for type: \"{type.FullName}\".");

            assignedType = null;
            return false;
        }

        private void Initialize ()
        {
            if (cachedEditorType == null)
                cachedEditorType = this.GetType();

            if (cachedAssignedType == null)
                if (!TryGetAssignedCustomEditorType(cachedEditorType, out cachedAssignedType))
                    return;

            if (cachedDefaultCustomEditorType == null)
            {
                if ((cachedDefaultCustomEditorType = cachedTypesWithCustomEditorAttribute.Where(type =>
                {
                    if (!TryGetAssignedCustomEditorType(type, out var assignedType, throwError: false))
                        return false;
                    return assignedType == cachedAssignedType;

                }).FirstOrDefault()) == null)
                    return;
            }

            if (cachedMethods.Count == 0)
            {
                for (int i = 0; i < TargetMethodStrings.Length; i++)
                {
                    var method = cachedDefaultCustomEditorType.GetMethod(TargetMethodStrings[i], BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null)
                    {
                        // Debug.LogError($"Unable to find method: \"{TargetMethodStrings[i]}\" in custom editor: \"{cachedDefaultCustomEditorType.FullName}\".");
                        continue;
                    }

                    if (!cachedMethods.ContainsKey(TargetMethodStrings[i]))
                        cachedMethods.Add(TargetMethodStrings[i], method);
                }
            }
        }

        protected abstract void OnExtendInspectorGUI(InstanceType instance);
        protected abstract void OnPollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered);

        public void ExtendInspectorGUI(InstanceType instance) => OnExtendInspectorGUI(instance);
        public void PollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered) => OnPollReflectorGUI(instance, anyStreamablesRegistered);
        public override void OnInspectorGUI()
        {
            if (cachedEditorInstance == null)
                return;

            if (UseDefaultInspector)
                cachedEditorInstance.OnInspectorGUI();

            PollExtendInspectorGUI<IInspectorExtension<InstanceType>, InstanceType>(this);
        }

        public void OnSceneGUI() => TryCallCachedMethod(OnSceneGUIMethodString, throwError: false);
        protected override void OnHeaderGUI() => TryCallCachedMethod(OnHeaderGUIMethodString, throwError: false);
        protected override bool ShouldHideOpenButton() => TryCallCachedMethod<bool>(ShouldHideOpenButtonMethodString, throwError: false);

        public override VisualElement CreateInspectorGUI() => EditorInstance.CreateInspectorGUI();
        public override void DrawPreview(Rect previewArea) => EditorInstance.DrawPreview(previewArea);
        public override string GetInfoString() => EditorInstance.GetInfoString();
        public override GUIContent GetPreviewTitle() => EditorInstance.GetPreviewTitle();
        public override bool HasPreviewGUI() => EditorInstance.HasPreviewGUI();
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background) => EditorInstance.OnInteractivePreviewGUI(r, background);
        public override void OnPreviewGUI(Rect r, GUIStyle background) => EditorInstance.OnPreviewGUI(r, background);
        public override void OnPreviewSettings() => EditorInstance.OnPreviewSettings();
        public override void ReloadPreviewInstances() => EditorInstance.ReloadPreviewInstances();
        public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height) => EditorInstance.RenderStaticPreview(assetPath, subAssets, width, height);
        public override bool RequiresConstantRepaint() => EditorInstance.RequiresConstantRepaint();
        public override bool UseDefaultMargins() => EditorInstance.UseDefaultMargins();
    }
}
