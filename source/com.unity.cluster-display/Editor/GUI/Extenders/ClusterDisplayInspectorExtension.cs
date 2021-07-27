using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

using MethodGUIFunc = System.Func<UnityEngine.Component, System.Object[], System.Object[]>;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public interface IInspectorExtension<InstanceType>
        where InstanceType : Component
    {
        void ExtendInspectorGUI(InstanceType instance);
        void PollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered);
    }

    public struct MethodMemberData
    {
        public bool isRegistered;

        public UnityEngine.Component wrappedInstance;

        public MethodInfo methodInfo;
        public string methodString;

        public UnityEngine.Component wrapperInstance;
        public MethodInfo wrapperEquivalentMethodInfo;

        public bool expand;
        public MethodGUIFunc onGUI;
    }

    public struct PropertyMemberData
    {
        public bool isRegistered;

        public UnityEngine.Component targetInstance; 

        public PropertyInfo propertyInfo;
        public string propertyString;

        public UnityEngine.Component wrapperInstance;
        public PropertyInfo wrapperEquivalentProperty;

        public bool expand;
        public Action<UnityEngine.Component> onGUI;
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
        private Dictionary<PropertyInfo, PropertyMemberData> cachedPropertyData = new Dictionary<PropertyInfo, PropertyMemberData>();
        private Dictionary<MethodInfo, MethodMemberData> cachedMethodData = new Dictionary<MethodInfo, MethodMemberData>();

        public ClusterDisplayInspectorExtension(bool useDefaultInspector = true) => this.useDefaultInspector = useDefaultInspector;

        private void SelectMonoScriptViaType (System.Type type) =>
            Selection.objects = new UnityEngine.Object[1] { AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(AssetDatabase
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
            if ((foldOutProperties = EditorGUILayout.Foldout(foldOutProperties, "Properties with Setters")) && PollProperties(
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

        private bool ExpandButtonGUI (bool eventRegistered)
        {
            if (!eventRegistered)
                return false;

            var cachedPreviousColor = GUI.backgroundColor;
            // GUI.backgroundColor = eventRegistered ? Color.green : Color.red;
            var selected = GUILayout.Button(new GUIContent("", "Expand event invocation menu"), EditorStyles.foldout, GUILayout.Width(20));
            GUI.backgroundColor = cachedPreviousColor;

            return selected;
        }

        private bool EventButtonGUI (bool eventRegistered)
        {
            var cachedPreviousColor = GUI.backgroundColor;
            bool selected = false;

            if (Application.isPlaying)
            {
                GUI.backgroundColor = eventRegistered ? Color.green * 0.65f : Color.red * 0.65f;
                GUILayout.Button(new GUIContent(eventRegistered ? "X" : "→", "Stop the game to edit."), GUILayout.Width(20));
            }

            else
            {
                GUI.backgroundColor = eventRegistered ? Color.green : Color.red;
                selected = GUILayout.Button(new GUIContent(eventRegistered ? "X" : "→", eventRegistered ? "Remove as cluster display event" : "Add as cluster display event."), GUILayout.Width(20));
            }

            GUI.backgroundColor = cachedPreviousColor;

            return selected;
        }

        private bool MethodInvocationUI (MethodInfo MethodInfo, UnityEngine.Object instance)
        {
            return false;
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
                    if (EventButtonGUI(false))
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
                var wrapperInstance = targetInstance.gameObject.GetComponent(wrapperType);
                // Create a <MethodInfo, <MethodInfo, string>> dictionary from the methods we find.
                cachedMethodData = cachedMethods.ToDictionary(
                    cachedMethod => cachedMethod,
                    cachedMethod =>
                    {
                        // Using the property in our target instance, find equivalent property in the type that wraps our target type.

                        MethodGUIFunc onGUI = null;
                        bool isRegistered = false;

                        if (ReflectionUtils.TryFindMethodWithMatchingSignature(wrapperType, cachedMethod, out var wrapperMethod))
                        {
                            isRegistered = RPCRegistry.MethodRegistered(wrapperMethod);
                            if (isRegistered)
                                ClusterDisplayInspectorUtils.TryCreateMethodGUI(wrapperMethod, wrapperInstance, out onGUI);
                        }

                        else isRegistered = RPCRegistry.MethodRegistered(cachedMethod);

                        // Store the nullable wrapper property and property string in this tuple as a value in our dictionary.
                        return new MethodMemberData
                        {
                            isRegistered = isRegistered,

                            wrappedInstance = targetInstance,

                            methodInfo = cachedMethod,
                            methodString = PrettyMethodString(cachedMethod),

                            wrapperInstance = wrapperInstance,
                            wrapperEquivalentMethodInfo = wrapperMethod,

                            onGUI = onGUI,
                            expand = false,
                        };
                    });

                return;
            }

            // If our wrapper does not exist, then we just use our target type methods instead.
            cachedMethodData = cachedMethods.ToDictionary(
                cachedMethod => cachedMethod,
                cachedMethod => 
                {
                    bool isRegistered = RPCRegistry.MethodRegistered(cachedMethod);
                    MethodGUIFunc onGUI = null;
                    if (isRegistered)
                        ClusterDisplayInspectorUtils.TryCreateMethodGUI(cachedMethod, targetInstance, out onGUI);

                    return new MethodMemberData
                    {
                        isRegistered = isRegistered,
                        wrappedInstance = targetInstance,

                        methodInfo = cachedMethod,
                        methodString = PrettyMethodString(cachedMethod),

                        wrapperInstance = null,
                        wrapperEquivalentMethodInfo = null,

                        onGUI = onGUI,
                        expand = false,
                    };
                });
        }

        private void CachePropertiesAndDescriptors<InstanceType> (InstanceType targetInstance)
            where InstanceType : Component
        {
            cachedProperties = ReflectionUtils.GetAllPropertySetMethods(targetInstance.GetType(), propertySearchStr, valueTypesOnly: true, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            // If our wrapper type exists, then we should be using our wrapper properties instead.
            if (TryGetDirectOrWrapperType(targetInstance, out var wrapperType))
            {
                var wrapperInstance = targetInstance.gameObject.GetComponent(wrapperType);

                // Create a <PropertyInfo, <PropertyInfo, string>> dictionary from the properties we find.
                cachedPropertyData = cachedProperties.ToDictionary(
                    cachedPropertyData => cachedPropertyData.property,
                    cachedPropertyData => {

                        // Using the property in our target instance, find equivalent property in the type that wraps our target type.
                        Action<Component> onGUI = null;
                        bool isRegistered = false;

                        if (ReflectionUtils.TryFindPropertyWithMatchingSignature(wrapperType, cachedPropertyData.property, matchGetSetters: false, out var wrapperProperty))
                        {
                            isRegistered = RPCRegistry.MethodRegistered(wrapperProperty.SetMethod);
                            if (isRegistered)
                                ClusterDisplayInspectorUtils.TryCreatePropertyGUI(wrapperProperty, wrapperInstance, out onGUI);
                        }

                        else isRegistered = RPCRegistry.MethodRegistered(cachedPropertyData.property.SetMethod);

                        // Store the nullable wrapper property and property string in this tuple as a value in our dictionary.
                        return new PropertyMemberData
                        {
                            isRegistered = isRegistered,

                            targetInstance = targetInstance,

                            propertyInfo = cachedPropertyData.property,
                            propertyString = PrettyPropertyName(cachedPropertyData.property),

                            wrapperInstance = wrapperInstance,
                            wrapperEquivalentProperty = wrapperProperty,

                            onGUI = onGUI,
                            expand = false,
                        };
                    });

                return;
            }

            // If our wrapper does not exist, then we just use our target type properties instead.
            cachedPropertyData = cachedProperties.ToDictionary(
                cachedPropertyData => cachedPropertyData.property,
                cachedPropertyData => {

                    bool isRegistered = RPCRegistry.MethodRegistered(cachedPropertyData.property.SetMethod);
                    Action<Component> onGUI = null;
                    if (isRegistered)
                        ClusterDisplayInspectorUtils.TryCreatePropertyGUI(cachedPropertyData.property, targetInstance, out onGUI);

                    return new PropertyMemberData
                    {
                        isRegistered = isRegistered,
                        targetInstance = targetInstance,

                        propertyInfo = cachedPropertyData.property,
                        propertyString = PrettyPropertyName(cachedPropertyData.property),

                        wrapperInstance = null,
                        wrapperEquivalentProperty = null,

                        onGUI = onGUI,
                        expand = false
                    };
                });
        }

        private readonly Dictionary<Component, Dictionary<MethodInfo, object[]>> cachedArgs = new Dictionary<Component, Dictionary<MethodInfo, object[]>>();
        protected bool PollMethods<InstanceType> (
            InstanceType targetInstance, 
            out System.Reflection.MethodInfo methodToRegister, 
            out SelectedState selectedState, 
            ref bool hasRegistered)
            where InstanceType : Component
        {
            methodToRegister = null;
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
                    var methodData = cachedMethodData[cachedMethods[i]];

                    // Determine whether a wrapper method exists, and if it does, use that one instead, otherwise use the target instance method.
                    bool usingWrapper = methodData.wrapperEquivalentMethodInfo != null;
                    var selectedMethod = usingWrapper ? methodData.wrapperEquivalentMethodInfo : cachedMethods[i];

                    hasRegistered |= methodData.isRegistered;

                    EditorGUILayout.BeginHorizontal();

                    if (methodData.onGUI != null && ExpandButtonGUI(methodData.isRegistered))
                        methodData.expand = !methodData.expand;

                    if (EventButtonGUI(methodData.isRegistered))
                    {
                        if (methodData.isRegistered)
                        {
                            RPCRegistry.RemoveRPC(selectedMethod);
                            methodToRegister = selectedMethod;
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(selectedMethod))
                        {
                            methodToRegister = selectedMethod;
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (methodData.isRegistered)
                    {
                        if (RPCRegistry.TryGetRPC(selectedMethod, out var rpcMethodInfo))
                            RPCToggleGUI(targetInstance, ref rpcMethodInfo);
                    }

                    InfoGUI(methodData.methodString, usingWrapper, selectedMethod.DeclaringType);
                    EditorGUILayout.EndHorizontal();

                    if (methodData.expand)
                    {
                        if (methodData.onGUI != null)
                        {
                            if (cachedArgs.TryGetValue(targetInstance, out var instanceArgs))
                            {
                                if (instanceArgs.TryGetValue(methodData.methodInfo, out var args))
                                {
                                    args = methodData.onGUI(usingWrapper ? methodData.wrapperInstance : targetInstance, args);
                                    if (args != null)
                                        instanceArgs[methodData.methodInfo] = args;
                                }

                                else
                                {
                                    args = methodData.onGUI(usingWrapper ? methodData.wrapperInstance : targetInstance, null);
                                    if (args != null)
                                        instanceArgs.Add(methodData.methodInfo, args);
                                }
                            }
                            else
                            {
                                var args = methodData.onGUI(usingWrapper ? methodData.wrapperInstance : targetInstance, null);
                                if (args != null)
                                    cachedArgs.Add(targetInstance, new Dictionary<MethodInfo, object[]>() {{methodData.methodInfo, args}});
                            }
                        }
                    }

                    cachedMethodData[cachedMethods[i]] = methodData;
                }

                EndScrollView();
            }

            else EditorGUILayout.LabelField("No Methods Available");
            EndTab();

            return selectedState != SelectedState.None && methodToRegister != null;
        }

        protected bool PollProperties<InstanceType> (
            InstanceType targetInstance, 
            out PropertyInfo propertyToRegister, 
            out SelectedState selectedState,
            ref bool hasRegistered)
            where InstanceType : Component
        {
            propertyToRegister = null;
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
                    var propertyData = cachedPropertyData[cachedProperties[i].property];

                    // Determine whether a wrapper property exists, and if it does, use that one instead, otherwise use the target instance property.
                    bool usingWrapper = propertyData.wrapperEquivalentProperty != null;
                    var selectedProperty = usingWrapper ? propertyData.wrapperEquivalentProperty : cachedProperties[i].property;
                    hasRegistered |= propertyData.isRegistered;

                    EditorGUILayout.BeginHorizontal();

                    if (propertyData.onGUI != null && ExpandButtonGUI(propertyData.isRegistered))
                        propertyData.expand = !propertyData.expand;

                    if (EventButtonGUI(propertyData.isRegistered))
                    {
                        if (propertyData.isRegistered)
                        {
                            RPCRegistry.RemoveRPC(selectedProperty.SetMethod);
                            propertyToRegister = selectedProperty;
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(selectedProperty))
                        {
                            propertyToRegister = selectedProperty;
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (propertyData.isRegistered)
                    {
                        if (RPCRegistry.TryGetRPC(selectedProperty.SetMethod, out var rpcMethodInfo))
                            RPCToggleGUI(targetInstance, ref rpcMethodInfo);
                    }

                    InfoGUI(propertyData.propertyString, usingWrapper, selectedProperty.DeclaringType);

                    EditorGUILayout.EndHorizontal();

                    if (propertyData.expand)
                        if (propertyData.onGUI != null)
                            propertyData.onGUI(usingWrapper ? propertyData.wrapperInstance : targetInstance);

                    cachedPropertyData[cachedProperties[i].property] = propertyData;
                }

                EndScrollView();
            }

            else EditorGUILayout.LabelField("No Properties Available");
            EndTab();

            return selectedState != SelectedState.None && propertyToRegister != null && propertyToRegister.SetMethod != null;
        }
    }
}
