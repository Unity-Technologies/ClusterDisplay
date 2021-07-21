using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public interface IInspectorExtension<InstanceType>
        where InstanceType : Component
    {
        void ExtendInspectorGUI(InstanceType instance);
        void PollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered);
    }

    public abstract class ClusterDisplayInspectorExtension : UnityEditor.Editor
    {
        public const string GeneratedInspectorNamespace = "Unity.ClusterDisplay.Editor.Inspectors.Generated";

        protected enum SelectedState
        {
            None,
            Added,
            Removed
        }

        private bool foldOut = false;

        protected bool UseDefaultInspector => useDefaultInspector;
        private readonly bool useDefaultInspector;

        private const int maxListSizeBeforeScroll = 200;
        private bool usingScrollView = false;

        private bool foldOutFields;
        private bool foldOutMethods;
        private bool foldOutProperties;

        private string fieldSearchStr;
        private string methodSearchStr;
        private string propertySearchStr;

        private Vector2 fieldScrollPosition;
        private Vector2 methodScrollPosition;
        private Vector2 propertyScrollPosition;

        private SerializedProperty[] cachedFields;
        private MethodInfo[] cachedMethods;
        private (PropertyInfo property, MethodInfo setMethod)[] cachedProperties;

        private Dictionary<SerializedProperty, string> cachedFieldDescriptors = new Dictionary<SerializedProperty, string>();
        private Dictionary<PropertyInfo, (PropertyInfo wrapperEquivalentProperty, string propertyString)> cachedPropertyDescriptors = new Dictionary<PropertyInfo, (PropertyInfo wrapperEquivalentProperty, string propertyString)>();
        private Dictionary<MethodInfo, (MethodInfo wrapperEquivalentMethod, string methodString)> cachedMethodDescriptors = new Dictionary<MethodInfo, (MethodInfo wrapperEquivalentMethod, string methodString)>();

        public ClusterDisplayInspectorExtension(bool useDefaultInspector = true) => this.useDefaultInspector = useDefaultInspector;

        private void SelectMonoScriptViaType (System.Type type) =>
            Selection.objects = new Object[1] { AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(AssetDatabase
                .FindAssets("t:MonoScript")
                .FirstOrDefault(ms =>
                    AssetDatabase.LoadAssetAtPath<MonoScript>(
                        AssetDatabase.GUIDToAssetPath(ms)).GetClass() == type))) };

        /// <summary>
        /// This method generically casts our target to our instance type then performs
        /// base functions such as determining whether the cluster display foldout should be shown.
        /// </summary>
        /// <typeparam name="EditorType">The custom editor type for our instance.</typeparam>
        /// <typeparam name="InstanceType">The instance type we are extending the inspector for.</typeparam>
        /// <param name="interfaceInstance"></param>
        protected void ExtendInspectorGUIWithClusterDisplay<EditorType, InstanceType> (EditorType interfaceInstance)
            where EditorType : IInspectorExtension<InstanceType>
            where InstanceType : Component
        {
            var instance = target as InstanceType;
            if (instance == null)
                return;

            // Only present the cluster display UI for instances that the wrapper wraps, don't display it for wrappers.
            if (WrapperUtils.IsWrapper(target.GetType()))
                return;

            if (foldOut = EditorGUILayout.Foldout(foldOut, "Cluster Display"))
            {
                PresentStreamables(instance, out var hasRegistered);
                interfaceInstance.PollReflectorGUI(instance, hasRegistered);
                interfaceInstance.ExtendInspectorGUI(instance);
            }
        }

        protected bool TryGetWrapperInstance<InstanceType, WrapperType> (InstanceType instance, ref WrapperType wrapperInstance) 
            where InstanceType : Component 
            where WrapperType : ComponentWrapper<InstanceType>
        {
            if (wrapperInstance == null)
            {
                var wrapperType = typeof(WrapperType);
                var wrappers = instance.gameObject.GetComponents<WrapperType>();
                foreach (var wrapper in wrappers)
                {
                    if (!wrapper.TryGetInstance(out var wrappedInstance))
                        continue;

                    if (wrappedInstance != instance)
                        continue;

                    wrapperInstance = wrapper;
                    return true;
                }
            }

            return wrapperInstance != null;
        }

        protected void PresentStreamables<InstanceType> (InstanceType instance, out bool anyStreamablesRegistered)
            where InstanceType : Component
        {
            // EditorGUILayout.LabelField("Streamables", EditorStyles.boldLabel);

            BeginTab();
            anyStreamablesRegistered = false;

            if ((foldOutFields = EditorGUILayout.Foldout(foldOutFields, "Fields")) && PollFields(
                instance, 
                out var selectedField, 
                out var selectedState, 
                ref anyStreamablesRegistered))
            {
            }

            RPCEditorGUICommon.HorizontalLine();
            if ((foldOutProperties = EditorGUILayout.Foldout(foldOutProperties, "Properties")) && PollProperties(
                instance, 
                out var selectedProperty, 
                out selectedState, 
                ref anyStreamablesRegistered))
            {
            }

            RPCEditorGUICommon.HorizontalLine();
            if ((foldOutMethods = EditorGUILayout.Foldout(foldOutMethods, "Methods")) && PollMethods(
                instance, 
                out var selectedMethodInfo, 
                out selectedState, 
                ref anyStreamablesRegistered))
            {
            }

            RPCEditorGUICommon.HorizontalLine();

            EndTab();
        }

        protected bool WrapperButton<WrapperType, InstanceType> (InstanceType instance, ref WrapperType wrapperInstance)
            where InstanceType : Component
            where WrapperType : ComponentWrapper<InstanceType> 
        {
            var cachedBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = wrapperInstance == null ? Color.green : Color.red;
            if (GUILayout.Button(wrapperInstance == null ? "Create Wrapper" : "Remove Wrapper"))
            {
                if (wrapperInstance == null)
                {
                    if (WrapperUtils.TryFindWrapperImplementationType<InstanceType, WrapperType>(out var wrapperImplementationType))
                    {
                        wrapperInstance = instance.gameObject.AddComponent(wrapperImplementationType) as WrapperType;
                        wrapperInstance.SetInstance(instance);
                    }
                }

                else DestroyImmediate(wrapperInstance);
                return true;
            }

            GUI.backgroundColor = cachedBackgroundColor;

            return false;
        }

        private void BeginScrollView (int count, ref Vector2 scrollPosition)
        {
            float totalHeight = count * EditorGUIUtility.singleLineHeight; 
            usingScrollView = totalHeight > maxListSizeBeforeScroll;
            if (usingScrollView)
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(maxListSizeBeforeScroll));
        }

        private void EndScrollView ()
        {
            if (usingScrollView)
                EditorGUILayout.EndScrollView();
        }

        private void BeginTab ()
        {
            EditorGUILayout.BeginHorizontal();
            // EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical();
        }

        private void EndTab ()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private bool PollSearch (string title, ref string currentSearchStr)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(100));

            var newFieldSearchStr = EditorGUILayout.TextField(currentSearchStr);
            bool changed = currentSearchStr != newFieldSearchStr;
            currentSearchStr = newFieldSearchStr;

            EditorGUILayout.EndHorizontal();
            return changed;
        }

        private void RPCToggleGUI (Component instance, ref RPCMethodInfo rpcMethodInfo)
        {
            // EditorGUILayout.LabelField(text);
            if (!SceneObjectsRegistry.TryGetPipeId(instance, out var pipeId))
                return;

            var rpcConfig = SceneObjectsRegistry.GetRPCConfig(pipeId, rpcMethodInfo.rpcId);
            bool newState = EditorGUILayout.Toggle(rpcConfig.enabled, GUILayout.Width(15));

            if (rpcConfig.enabled != newState)
            {
                rpcConfig.enabled = newState;
                if (SceneObjectsRegistry.TryGetSceneInstance(instance.gameObject.scene.path, out var sceneObjectsRegistry))
                    sceneObjectsRegistry.UpdateRPCConfig(pipeId, rpcMethodInfo.rpcId, ref rpcConfig);
            }
        }

        private void DescriptorGUI (string text, out Vector2 textSize)
        {
            textSize = GUI.skin.label.CalcSize(new GUIContent(text));
            EditorGUILayout.LabelField(text, GUILayout.Width(textSize.x));
        }

        private void InfoGUI (string text, bool usingWrapper = false, System.Type wrapperType = null)
        {
            EditorGUILayout.BeginHorizontal();
            DescriptorGUI(text, out var textSize);
            if (usingWrapper)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Wrapper", GUILayout.Width(75)))
                    SelectMonoScriptViaType(wrapperType);
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool ButtonGUI (bool registered)
        {
            var cachedPreviousColor = GUI.backgroundColor;
            GUI.backgroundColor = registered ? Color.green : Color.red;
            bool selected = GUILayout.Button(registered ? "→" : "→", GUILayout.Width(20));
            GUI.backgroundColor = cachedPreviousColor;

            return selected;
        }

        private string PrettyMethodString (MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();
            if (parameters.Length > 0)
                return $"{methodInfo.ReturnType.Name} {methodInfo.Name} ({(parameters.Select(parameter => parameter.ParameterType.Name).Aggregate((aggregation, next) => $"{aggregation}, {next}"))})";
            return $"{methodInfo.ReturnType.Name} {methodInfo.Name} ()";
        }

        private string PrettyPropertyName(PropertyInfo propertyInfo) => $"{propertyInfo.PropertyType.Name} {propertyInfo.Name}";

        private bool TryGetDirectOrWrapperType<InstanceType> (InstanceType targetInstance, out System.Type wrapperType)
            where InstanceType : Component
        {
            var targetInstanceType = targetInstance.GetType();
            wrapperType = null;

            if (!ReflectionUtils.IsAssemblyPostProcessable(targetInstanceType.Assembly))
                return WrapperUtils.TryGetWrapperForType(targetInstanceType, out wrapperType);
            return false;
        }

        private void CacheFields<InstanceType> (InstanceType targetInstance)
            where InstanceType : Component
        {
            if (!TryGetDirectOrWrapperType(targetInstance, out var targetInstanceType))
                return;

            var type = targetInstance.GetType();
            var fields = ReflectionUtils.GetAllFieldsFromType(type, fieldSearchStr, valueTypesOnly: true, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            List<SerializedProperty> serializedProperties = new List<SerializedProperty>();
            var serializedObject = new SerializedObject(targetInstance);

            if (fields != null)
                for (int i = 0; i < fields.Length; i++)
                {
                    var serializedProperty = serializedObject.FindProperty(fields[i].Name);
                    if (serializedProperty == null)
                        continue;
                    serializedProperties.Add(serializedProperty);
                }

            cachedFields = serializedProperties.ToArray();
            cachedFieldDescriptors = cachedFields.ToDictionary(
                serializedProperty => serializedProperty,
                serializedProperty => $"{serializedProperty.type} {serializedProperty.name}");
        }

        protected bool PollFields<InstanceType> (
            InstanceType targetInstance, 
            out SerializedProperty selectedField, 
            out SelectedState selectedState, 
            ref bool hasRegistered)
            where InstanceType : Component
        {
            selectedField = null;
            selectedState = SelectedState.None;

            if (targetInstance == null)
            {
                hasRegistered = false;
                return false;
            }

            BeginTab();
            if (cachedFields == null || PollSearch("Filter Fields:", ref fieldSearchStr))
                CacheFields(targetInstance);

            if (cachedFields != null && cachedFields.Length > 0)
            {
                BeginScrollView(cachedFields.Length, ref fieldScrollPosition);
                for (int i = 0; i < cachedFields.Length; i++)
                {
                    var fieldDescriptor = cachedFieldDescriptors[cachedFields[i]];
                    EditorGUILayout.BeginHorizontal();
                    if (ButtonGUI(false))
                    {
                    }
                    InfoGUI(fieldDescriptor, false, null);
                    EditorGUILayout.EndHorizontal();
                }

                EndScrollView();
            }

            else EditorGUILayout.LabelField("No Fields Available");
            EndTab();

            hasRegistered = false;
            return true;
        }

        private void CacheMethodsAndDescriptors<InstanceType> (InstanceType targetInstance)
            where InstanceType : Component
        {
            cachedMethods = ReflectionUtils.GetAllMethodsFromType(targetInstance.GetType(), methodSearchStr, valueTypeParametersOnly: true, includeGenerics: false, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            // If our wrapper type exists, then we should be using our wrapper methods instead.
            if (TryGetDirectOrWrapperType(targetInstance, out var wrapperType))
            {
                // Create a <MethodInfo, <MethodInfo, string>> dictionary from the methods we find.
                cachedMethodDescriptors = cachedMethods.ToDictionary(
                    cachedMethod => cachedMethod,
                    cachedMethod => 
                    {
                        // Using the method in our target instance, find equivalent method in the type that wraps our target type.
                        ReflectionUtils.TryFindMethodWithMatchingSignature(wrapperType, cachedMethod, out var wrapperMethod);

                        // Store the nullable wrapper method and method string in this tuple as a value in our dictionary.
                        (MethodInfo wrapperEquivalentMethod, string parameters) tuple =
                        (
                            wrapperMethod,
                            PrettyMethodString(cachedMethod)
                        );
                        return tuple;
                    });

                return;
            }

            // If our wrapper does not exist, then we just use our target type methods instead.
            cachedMethodDescriptors = cachedMethods.ToDictionary(
                cachedMethod => cachedMethod,
                cachedMethod => 
                {
                    (MethodInfo wrapperEquivalentMethod, string parameters) tuple =
                    (
                        null,
                        PrettyMethodString(cachedMethod)
                    );
                    return tuple;
                });
        }

        protected bool PollMethods<InstanceType> (
            InstanceType targetInstance, 
            out System.Reflection.MethodInfo selectedMethodInfo, 
            out SelectedState selectedState, 
            ref bool hasRegistered)
            where InstanceType : Component
        {
            selectedMethodInfo = null;
            selectedState = SelectedState.None;

            if (targetInstance == null)
                return false;

            BeginTab();
            if (cachedMethods == null || PollSearch("Filter Methods:", ref methodSearchStr))
                CacheMethodsAndDescriptors(targetInstance);

            if (cachedMethods != null && cachedMethods.Length > 0)
            {
                BeginScrollView(cachedMethods.Length, ref methodScrollPosition);
                for (int i = 0; i < cachedMethods.Length; i++)
                {
                    var methodDescriptor = cachedMethodDescriptors[cachedMethods[i]];

                    // Determine whether a wrapper method exists, and if it does, use that one instead, otherwise use the target instance method.
                    bool usingWrapper = methodDescriptor.wrapperEquivalentMethod != null;
                    var method = usingWrapper ? methodDescriptor.wrapperEquivalentMethod : cachedMethods[i];

                    bool registered = RPCRegistry.TryGetRPC(method, out var rpcMethodInfo);
                    hasRegistered |= registered;

                    EditorGUILayout.BeginHorizontal();

                    if (ButtonGUI(registered))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(method);
                            selectedMethodInfo = method;
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(method))
                        {
                            selectedMethodInfo = method;
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (registered)
                        RPCToggleGUI(targetInstance, ref rpcMethodInfo);
                    InfoGUI(methodDescriptor.methodString, usingWrapper, method.DeclaringType);
                    EditorGUILayout.EndHorizontal();
                }

                EndScrollView();
            }

            else EditorGUILayout.LabelField("No Methods Available");
            EndTab();

            return selectedState != SelectedState.None && selectedMethodInfo != null;
        }

        private void CachePropertiesAndDescriptors<InstanceType> (InstanceType targetInstance)
            where InstanceType : Component
        {
            cachedProperties = ReflectionUtils.GetAllPropertySetMethods(targetInstance.GetType(), propertySearchStr, valueTypesOnly: true, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            // If our wrapper type exists, then we should be using our wrapper properties instead.
            if (TryGetDirectOrWrapperType(targetInstance, out var wrapperType))
            {
                // Create a <PropertyInfo, <PropertyInfo, string>> dictionary from the properties we find.
                cachedPropertyDescriptors = cachedProperties.ToDictionary(
                    cachedPropertyData => cachedPropertyData.property,
                    cachedPropertyData => {

                        // Using the property in our target instance, find equivalent property in the type that wraps our target type.
                        ReflectionUtils.TryFindPropertyWithMatchingSignature(wrapperType, cachedPropertyData.property, matchGetSetters: false, out var wrapperProperty);

                        // Store the nullable wrapper property and property string in this tuple as a value in our dictionary.
                        (PropertyInfo wrapperEquivalentProperty, string parameters) tuple =
                        (
                            wrapperProperty,
                            PrettyPropertyName(cachedPropertyData.property)
                        );

                        return tuple;
                    });

                return;
            }

            // If our wrapper does not exist, then we just use our target type properties instead.
            cachedPropertyDescriptors = cachedProperties.ToDictionary(
                cachedPropertyData => cachedPropertyData.property,
                cachedPropertyData => {
                    (PropertyInfo wrapperEquivalentProperty, string parameters) tuple =
                    (
                        null,
                        PrettyPropertyName(cachedPropertyData.property)
                    );
                    return tuple;
                });
        }

        protected bool PollProperties<InstanceType> (
            InstanceType targetInstance, 
            out (PropertyInfo property, MethodInfo setMethod) selectedProperty, 
            out SelectedState selectedState,
            ref bool hasRegistered)
            where InstanceType : Component
        {
            selectedProperty = (null, null);
            selectedState = SelectedState.None;

            if (targetInstance == null)
                return false;

            BeginTab();
            if (cachedProperties == null || PollSearch("Filter Properties:", ref propertySearchStr))
                CachePropertiesAndDescriptors(targetInstance);

            if (cachedProperties != null && cachedProperties.Length > 0)
            {
                BeginScrollView(cachedProperties.Length, ref propertyScrollPosition);

                for (int i = 0; i < cachedProperties.Length; i++)
                {
                    var propertyDescriptor = cachedPropertyDescriptors[cachedProperties[i].property];

                    // Determine whether a wrapper property exists, and if it does, use that one instead, otherwise use the target instance property.
                    bool usingWrapper = propertyDescriptor.wrapperEquivalentProperty != null;
                    var property = usingWrapper ? propertyDescriptor.wrapperEquivalentProperty : cachedProperties[i].property;

                    bool registered = RPCRegistry.TryGetRPC(property.SetMethod, out var rpcMethodInfo);
                    hasRegistered |= registered;

                    EditorGUILayout.BeginHorizontal();

                    if (ButtonGUI(registered))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(property.SetMethod);
                            selectedProperty = (property, property.SetMethod);
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(property))
                        {
                            selectedProperty = (property, property.SetMethod);
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (registered)
                        RPCToggleGUI(targetInstance, ref rpcMethodInfo);
                    InfoGUI(propertyDescriptor.propertyString, usingWrapper, property.DeclaringType);
                    EditorGUILayout.EndHorizontal();
                }

                EndScrollView();
            }

            else EditorGUILayout.LabelField("No Properties Available");
            EndTab();

            return selectedState != SelectedState.None && selectedProperty.property != null && selectedProperty.setMethod != null;
        }
    }
}
