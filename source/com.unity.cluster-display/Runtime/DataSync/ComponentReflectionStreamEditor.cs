using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public partial class ComponentReflectionStream : MonoBehaviour, IPipeIDContainer, IDataWatcher
    {
        [CustomEditor(typeof(ComponentReflectionStream))]
        public class ComponentReflectorEditor : Editor 
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                GUILayout.Space(10);

                var componentReflector = target as ComponentReflectionStream;
                if (componentReflector == null || componentReflector.objects == null)
                    return;

                for (int oi = 0; oi < componentReflector.objects.Length; oi++)
                {
                    var obj = componentReflector.objects[oi];
                    if (obj == null)
                        continue;

                    var type = obj.GetType();

                    if (!componentReflector.states.TryGetValue(obj, out var state))
                        componentReflector.states.Add(obj, new ObjectUIState());

                    bool objFoldout = EditorGUILayout.Foldout(state.objFoldout, type.FullName);
                    if (objFoldout != state.objFoldout)
                    {
                        state.objFoldout = objFoldout;
                        componentReflector.states[obj] = state;
                    }

                    if (!state.objFoldout)
                        continue;

                    if (GUILayout.Button("Clear"))
                        componentReflector.Clear();

                    bool fieldsFoldout = EditorGUILayout.Foldout(state.fieldsFoldout, "Fields");
                    if (fieldsFoldout != state.fieldsFoldout)
                    {
                        state.fieldsFoldout = fieldsFoldout;
                        componentReflector.states[obj] = state;
                    }

                    (FieldInfo[] fields, PropertyInfo[] properties) = (null, null);
                    if (state.fieldsFoldout || state.propertiesFoldout)
                        (fields, properties) = ReflectionUtils.GetAllValueTypeFieldsAndProperties(type);

                    if (state.fieldsFoldout && fields != null)
                    {
                        for (int fi = 0; fi < fields.Length; fi++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(25);

                            if (GUILayout.Button("Benchmark"))
                            {
                                var boundTarget = componentReflector.targets[obj][fields[fi].Name].boundTarget;
                                ExpressionTreeUtils.BenchmarkExpression(obj, fields[fi], ref boundTarget);
                            }

                            if (!componentReflector.HasTarget(obj, fields[fi].Name))
                            {
                                if (GUILayout.Button("Add", GUILayout.Width(100)))
                                    componentReflector.Add(obj, fields[fi]);
                            }

                            else if (GUILayout.Button("Remove", GUILayout.Width(100)))
                                componentReflector.Remove(obj, fields[fi].Name);

                            EditorGUILayout.LabelField(fields[fi].Name, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(fields[fi].FieldType.Name);
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    bool propertiesFoldout = EditorGUILayout.Foldout(state.propertiesFoldout, "Properties");
                    if (propertiesFoldout != state.propertiesFoldout)
                    {
                        state.propertiesFoldout = propertiesFoldout;
                        componentReflector.states[obj] = state;
                    }

                    if (state.propertiesFoldout && properties != null)
                    {
                        for (int pi = 0; pi < properties.Length; pi++) 
                        { 
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(25);

                            if (GUILayout.Button("Benchmark"))
                            {
                                var boundTarget = componentReflector.targets[obj][properties[pi].Name].boundTarget;
                                ExpressionTreeUtils.BenchmarkExpression(obj, properties[pi], ref boundTarget);
                            }

                            if (!componentReflector.HasTarget(obj, properties[pi].Name))
                            {
                                if (GUILayout.Button("Add", GUILayout.Width(100)))
                                    componentReflector.Add(obj, properties[pi]);
                            }

                            else if (GUILayout.Button("Remove", GUILayout.Width(100)))
                                componentReflector.Remove(obj, properties[pi].Name);

                            EditorGUILayout.LabelField(properties[pi].Name, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(properties[pi].PropertyType.Name);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }

        [System.Serializable]
        public struct ObjectUIState
        {
            public bool objFoldout;
            public bool fieldsFoldout;
            public bool propertiesFoldout;
        }

        private readonly Dictionary<Object, ObjectUIState> states = new Dictionary<Object, ObjectUIState>();
        [HideInInspector][SerializeField] public ObjectUIState[] serializedStates;
    }
}

#endif
