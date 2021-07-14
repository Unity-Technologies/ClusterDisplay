using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.ClusterDisplay.RPC;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public interface IInspectorExtension<InstanceType>
        where InstanceType : Component
    {
        void ExtendInspectorGUI(InstanceType instance);
        void PollReflectorGUI(InstanceType instance, bool anyStreamablesRegistered);
    }

    public abstract class InspectorExtension : UnityEditor.Editor
    {
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

        public InspectorExtension(bool useDefaultInspector = true) => this.useDefaultInspector = useDefaultInspector;

        /// <summary>
        /// This method generically casts our target to our instance type then performs
        /// base functions such as determining whether the cluster display foldout should be shown.
        /// </summary>
        /// <typeparam name="EditorType">The custom editor type for our instance.</typeparam>
        /// <typeparam name="InstanceType">The instance type we are extending the inspector for.</typeparam>
        /// <param name="interfaceInstance"></param>
        protected void PollExtendInspectorGUI<EditorType, InstanceType> (EditorType interfaceInstance)
            where EditorType : IInspectorExtension<InstanceType>
            where InstanceType : Component
        {
            var instance = target as InstanceType;
            if (instance == null)
                return;

            if (foldOut = EditorGUILayout.Foldout(foldOut, "Cluster Display"))
            {
                PresentStreamables(instance, out var hasRegistered);
                interfaceInstance.PollReflectorGUI(instance, hasRegistered);
                interfaceInstance.ExtendInspectorGUI(instance);
            }
        }

        protected bool TryGetReflectorInstance<InstanceType, ReflectorType> (InstanceType instance, ref ReflectorType reflectorInstance) 
            where InstanceType : Component 
            where ReflectorType : ComponentReflector<InstanceType>
        {
            if (reflectorInstance == null)
            {
                var reflectors = instance.gameObject.GetComponents<ReflectorType>();
                foreach (var reflector in reflectors)
                {
                    if (reflector.TargetInstance != instance)
                        continue;

                    reflectorInstance = reflector;
                    return true;
                }
            }

            return reflectorInstance != null;
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

        protected bool ReflectorButton<ReflectorType, InstanceType> (InstanceType instance, ref ReflectorType reflectorInstance)
            where InstanceType : Component
            where ReflectorType : ComponentReflector<InstanceType> 
        {
            var cachedBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button(reflectorInstance == null ? "Create Reflector" : "Remove Reflector"))
            {
                if (reflectorInstance == null)
                {
                    reflectorInstance = instance.gameObject.AddComponent<ReflectorType>();
                    reflectorInstance.Setup(instance);
                }

                else DestroyImmediate(reflectorInstance);
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

        private void RPCToggle (Component instance, ref RPCMethodInfo rpcMethodInfo)
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

        private void Label (string text)
        {
            EditorGUILayout.LabelField(text);
        }

        private bool Button (bool registered)
        {
            var cachedPreviousColor = GUI.backgroundColor;
            GUI.backgroundColor = registered ? Color.green : Color.red;
            bool selected = GUILayout.Button(registered ? "→" : "→", GUILayout.Width(20));
            GUI.backgroundColor = cachedPreviousColor;

            return selected;
        }

        private string WrapperString(bool isWrapper) => isWrapper ? "[WRAPPER]" : "";

        private string PrettyMethodString (MethodInfo methodInfo, bool isWrapper)
        {
            var parameters = methodInfo.GetParameters();
            if (parameters.Length > 0)
                return $"{methodInfo.ReturnType.Name} {methodInfo.Name} ({(parameters.Select(parameter => parameter.ParameterType.Name).Aggregate((aggregation, next) => $"{aggregation}, {next}"))}) {WrapperString(isWrapper)}";
            return $"{methodInfo.ReturnType.Name} {methodInfo.Name} () {WrapperString(isWrapper)}";
        }

        private string PrettyPropertyName(PropertyInfo propertyInfo, bool isWrapper) => $"{propertyInfo.PropertyType.Name} {propertyInfo.Name} {WrapperString(isWrapper)}";

        private bool TryGetDirectOrWrapperType<InstanceType> (InstanceType targetInstance, out System.Type targetInstanceType)
            where InstanceType : Component
        {
            var defaultType = targetInstanceType = targetInstance.GetType();

            if (!ReflectionUtils.IsAssemblyPostProcessable(defaultType.Assembly))
                if (!WrapperUtils.TryGetWrapperForType(defaultType, out targetInstanceType))
                    targetInstanceType = defaultType;

            return targetInstanceType != null;
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
                    EditorGUILayout.BeginHorizontal();
                    if (Button(false))
                    {
                    }
                    Label(cachedFieldDescriptors[cachedFields[i]]);
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
            if (TryGetDirectOrWrapperType(targetInstance, out var wrapperType))
            {
                cachedMethodDescriptors = cachedMethods.ToDictionary(
                    cachedMethod => cachedMethod,
                    cachedMethod => 
                    {
                        ReflectionUtils.TryFindMethodWithMatchingSignature(wrapperType, cachedMethod, out var wrapperMethod);
                        (MethodInfo wrapperEquivalentMethod, string parameters) tuple =
                        (
                            wrapperMethod,
                            PrettyMethodString(cachedMethod, wrapperMethod != null)
                        );
                        return tuple;
                    });

                return;
            }

            cachedMethodDescriptors = cachedMethods.ToDictionary(
                cachedMethod => cachedMethod,
                cachedMethod => 
                {
                    (MethodInfo wrapperEquivalentMethod, string parameters) tuple =
                    (
                        null,
                        PrettyMethodString(cachedMethod, false)
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
                    bool registered = RPCRegistry.TryGetRPC(cachedMethods[i], out var rpcMethodInfo);
                    hasRegistered |= registered;

                    EditorGUILayout.BeginHorizontal();

                    if (Button(registered))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(cachedMethods[i]);
                            selectedMethodInfo = cachedMethods[i];
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(cachedMethods[i]))
                        {
                            selectedMethodInfo = cachedMethods[i];
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (registered)
                        RPCToggle(targetInstance, ref rpcMethodInfo);
                    Label(cachedMethodDescriptors[cachedMethods[i]].methodString);

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
            if (TryGetDirectOrWrapperType(targetInstance, out var wrapperType))
            {
                cachedPropertyDescriptors = cachedProperties.ToDictionary(
                    cachedPropertyData => cachedPropertyData.property,
                    cachedPropertyData => {

                        ReflectionUtils.TryFindPropertyWithMatchingSignature(wrapperType, cachedPropertyData.property, matchGetSetters: false, out var wrapperProperty);
                        (PropertyInfo wrapperEquivalentProperty, string parameters) tuple =
                        (
                            wrapperProperty,
                            PrettyPropertyName(cachedPropertyData.property, wrapperProperty != null)
                        );

                        return tuple;
                    });

                return;
            }

            cachedPropertyDescriptors = cachedProperties.ToDictionary(
                cachedPropertyData => cachedPropertyData.property,
                cachedPropertyData => {
                    (PropertyInfo wrapperEquivalentProperty, string parameters) tuple =
                    (
                        null,
                        PrettyPropertyName(cachedPropertyData.property, false)
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
                    bool registered = RPCRegistry.TryGetRPC(cachedProperties[i].setMethod, out var rpcMethodInfo);
                    hasRegistered |= registered;

                    EditorGUILayout.BeginHorizontal();

                    if (Button(registered))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(cachedProperties[i].setMethod);
                            selectedProperty = cachedProperties[i];
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(cachedProperties[i].property))
                        {
                            selectedProperty = cachedProperties[i];
                            selectedState = SelectedState.Added;
                        }
                    }

                    if (registered)
                        RPCToggle(targetInstance, ref rpcMethodInfo);
                    Label(cachedPropertyDescriptors[cachedProperties[i].property].propertyString);

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
