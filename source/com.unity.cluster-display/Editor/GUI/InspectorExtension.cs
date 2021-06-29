using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.ClusterDisplay.Networking;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor.Extensions
{
    public interface IInspectorExtension<InstanceType>
    {
        void ExtendInspectorGUI(InstanceType instance);
    }

    public abstract class InspectorExtension : UnityEditor.Editor
    {
        private bool foldOut = false;

        protected bool UseDefaultInspector => useDefaultInspector;
        private readonly bool useDefaultInspector;

        public InspectorExtension(bool useDefaultInspector)
        {
            this.useDefaultInspector = useDefaultInspector;
        }

        /// <summary>
        /// This method generically casts our target to our instance type then performs
        /// base functions such as determining whether the cluster display foldout should be shown.
        /// </summary>
        /// <typeparam name="EditorType">The custom editor type for our instance.</typeparam>
        /// <typeparam name="InstanceType">The instance type we are extending the inspector for.</typeparam>
        /// <param name="interfaceInstance"></param>
        protected void PollExtendInspectorGUI<EditorType, InstanceType> (EditorType interfaceInstance)
            where EditorType : IInspectorExtension<InstanceType>
            where InstanceType : Object
        {
            var instance = target as InstanceType;
            if (instance == null)
                return;

            if (foldOut = EditorGUILayout.Foldout(foldOut, "Cluster Display"))
                interfaceInstance.ExtendInspectorGUI(instance);
        }

        protected bool TryGetReflectorInstance<ReflectorType, InstanceType> (InstanceType instance, ref ReflectorType reflectorInstance) 
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

        protected bool ReflectorButton<ReflectorType, InstanceType> (InstanceType instance, ref ReflectorType reflectorInstance)
            where InstanceType : Component
            where ReflectorType : ComponentReflector<InstanceType> 
        {
            if (GUILayout.Button(reflectorInstance == null ? "Create Reflect" : "Remove Reflector"))
            {
                if (reflectorInstance == null)
                {
                    reflectorInstance = instance.gameObject.AddComponent<ReflectorType>();
                    reflectorInstance.Setup(instance);
                }

                else DestroyImmediate(reflectorInstance);
                return true;
            }

            return false;
        }

        protected enum SelectedState
        {
            None,
            Added,
            Removed
        }

        private const int maxListSizeBeforeScroll = 200;
        private bool usingScrollView = false;

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

        private bool PollSearch (string title, ref string currentSearchStr)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(75));
            var newFieldSearchStr = EditorGUILayout.TextField(currentSearchStr);
            EditorGUILayout.EndHorizontal();
            return currentSearchStr != newFieldSearchStr;
        }

        private bool Button (bool registered, string text)
        {
            EditorGUILayout.BeginHorizontal();
            var cachedPreviousColor = GUI.backgroundColor;
            GUI.backgroundColor = registered ? Color.green : Color.red;
            bool selected = GUILayout.Button(registered ? "→" : "→", GUILayout.Width(20));
            GUI.backgroundColor = cachedPreviousColor;
            EditorGUILayout.LabelField(text);
            EditorGUILayout.EndHorizontal();
            return selected;
        }
        private SerializedProperty[] cachedSerializedProperties;
        private string fieldSearchStr;
        private Vector2 fieldScrollPosition;

        private void PollFields<InstanceType> (InstanceType targetInstance)
            where InstanceType : Component
        {
            var type = targetInstance.GetType();
            var fields = ReflectionUtils.GetAllFieldsFromType(type, fieldSearchStr, valueTypesOnly: true, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (fields != null && fields.Length > 0)
            {
                List<SerializedProperty> serializedProperties = new List<SerializedProperty>();
                var serializedObject = new SerializedObject(targetInstance);

                for (int i = 0; i < fields.Length; i++)
                {
                    var serializedProperty = serializedObject.FindProperty(fields[i].Name);
                    if (serializedProperty == null)
                        continue;
                    serializedProperties.Add(serializedProperty);
                }

                cachedSerializedProperties = serializedProperties.ToArray();
            }
        }

        protected bool ListFields<InstanceType> (InstanceType targetInstance, out SerializedProperty selectedField, out SelectedState selectedState)
            where InstanceType : Component
        {
            selectedField = null;
            selectedState = SelectedState.None;

            if (targetInstance == null)
                return false;

            RPCEditorGUICommon.HorizontalLine();
            if (PollSearch("Fields", ref fieldSearchStr))
                PollFields(targetInstance);

            if (cachedSerializedProperties != null && cachedSerializedProperties.Length > 0)
            {
                BeginScrollView(cachedSerializedProperties.Length, ref fieldScrollPosition);
                for (int i = 0; i < cachedSerializedProperties.Length; i++)
                {
                    if (Button(false, cachedSerializedProperties[i].name))
                    {
                    }
                }

                EndScrollView();
            }

            return true;
        }

        private MethodInfo[] cachedMethods;
        private string methodSearchStr;
        private Vector2 methodScrollPosition;
        protected bool ListMethods<InstanceType> (InstanceType targetInstance, out System.Reflection.MethodInfo selectedMethodInfo, out SelectedState selectedState)
            where InstanceType : Component
        {
            selectedMethodInfo = null;
            selectedState = SelectedState.None;

            if (targetInstance == null)
                return false;

            RPCEditorGUICommon.HorizontalLine();
            if (cachedMethods == null || PollSearch("Methods", ref methodSearchStr))
                cachedMethods = ReflectionUtils.GetAllMethodsFromType(targetInstance.GetType(), methodSearchStr, valueTypeParametersOnly: true, includeGenerics: false, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            if (cachedMethods != null && cachedMethods.Length > 0)
            {
                BeginScrollView(cachedMethods.Length, ref methodScrollPosition);
                for (int i = 0; i < cachedMethods.Length; i++)
                {
                    bool registered = RPCRegistry.MethodRegistered(cachedMethods[i]);
                    if (Button(registered, cachedMethods[i].Name))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(cachedMethods[i]);
                            selectedMethodInfo = cachedMethods[i];
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(cachedMethods[i].DeclaringType, cachedMethods[i], RPCExecutionStage.Automatic, out var _))
                        {
                            selectedMethodInfo = cachedMethods[i];
                            selectedState = SelectedState.Added;
                        }
                    }
                }

                EndScrollView();
            }

            return selectedState != SelectedState.None && selectedMethodInfo != null;
        }

        private (PropertyInfo property, MethodInfo setMethod)[] cachedProperties;
        private string propertySearchStr;
        private Vector2 propertyScrollPosition;

        protected bool ListProperties<InstanceType> (InstanceType targetInstance, out (PropertyInfo property, MethodInfo setMethod) selectedProperty, out SelectedState selectedState)
            where InstanceType : Component
        {
            selectedProperty = (null, null);
            selectedState = SelectedState.None;
            if (targetInstance == null)
                return false;

            RPCEditorGUICommon.HorizontalLine();
            if (cachedProperties == null || PollSearch("Properties", ref propertySearchStr))
                cachedProperties = ReflectionUtils.GetAllPropertySetMethods(targetInstance.GetType(), propertySearchStr, valueTypesOnly: true, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            if (cachedProperties != null && cachedProperties.Length > 0)
            {
                BeginScrollView(cachedProperties.Length, ref propertyScrollPosition);

                for (int i = 0; i < cachedProperties.Length; i++)
                {
                    bool registered = RPCRegistry.MethodRegistered(cachedProperties[i].setMethod);
                    if (Button(registered, cachedProperties[i].property.Name))
                    {
                        if (registered)
                        {
                            RPCRegistry.RemoveRPC(cachedProperties[i].setMethod);
                            selectedProperty = cachedProperties[i];
                            selectedState = SelectedState.Removed;
                        }

                        else if (RPCRegistry.TryAddNewRPC(cachedProperties[i].property.DeclaringType, cachedProperties[i].setMethod, RPCExecutionStage.Automatic, out var _))
                        {
                            selectedProperty = cachedProperties[i];
                            selectedState = SelectedState.Added;
                        }
                    }
                }

                EndScrollView();
            }

            return selectedState != SelectedState.None && selectedProperty.property != null && selectedProperty.setMethod != null;
        }
    }
}
