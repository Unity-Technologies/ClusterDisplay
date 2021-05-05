using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public partial class RPCRegistry : ScriptableObject
    {
        [CustomEditor(typeof(RPCRegistry))]
        private class RPCRegistryEditor : Editor
        {
            private string typeSearchStr = "";
            private Type[] cachedTypes;
            private Type targetType;
            private Vector2 typeListScrollPosition;

            private string methodSearchStr = "";
            private MethodInfo[] cachedMethods;
            private MethodInfo targetMethod;
            private Vector2 methodListScrollPosition;

            private Vector2 registeredMethodListPosition;

            private void HorizontalLine () => EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            private void UpdateTypeSearch (string newClassSearchStr, bool forceUpdate = false)
            {
                if (!forceUpdate && newClassSearchStr == typeSearchStr)
                    return;

                typeSearchStr = newClassSearchStr;
                cachedTypes = ReflectionUtils.GetAllTypes(typeSearchStr, includeGenerics: false);
            }

            private void UpdateMethodSearch (string newMethodSearchStr, bool forceUpdate = false)
            {
                if (!forceUpdate && newMethodSearchStr == methodSearchStr)
                    return;

                methodSearchStr = newMethodSearchStr;
                cachedMethods = ReflectionUtils.GetAllMethodsFromType(targetType, methodSearchStr, includeGenerics: false);
            }

            private void ListTypes ()
            {
                if (cachedTypes == null)
                    UpdateTypeSearch(typeSearchStr, forceUpdate: true);

                if (cachedTypes.Length > 0)
                {
                    EditorGUILayout.LabelField("Types", EditorStyles.boldLabel);
                    typeListScrollPosition = EditorGUILayout.BeginScrollView(typeListScrollPosition, GUILayout.Height(150));
                    for (int i = 0; i < cachedTypes.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Target", GUILayout.Width(60)))
                        {
                            targetType = cachedTypes[i];
                            UpdateMethodSearch(methodSearchStr, forceUpdate: true);
                        }

                        EditorGUILayout.LabelField(cachedTypes[i].FullName);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }

                else EditorGUILayout.LabelField($"No types found from search: \"{typeSearchStr}\".");
            }

            private void ListMethods ()
            {
                if (cachedMethods == null)
                    UpdateMethodSearch(methodSearchStr, forceUpdate: true);

                if (cachedMethods.Length > 0)
                {
                    EditorGUILayout.LabelField("Methods", EditorStyles.boldLabel);
                    methodListScrollPosition = EditorGUILayout.BeginScrollView(methodListScrollPosition, GUILayout.Height(150));
                    for (int i = 0; i < cachedMethods.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            targetMethod = cachedMethods[i];
                            (target as RPCRegistry).TryRegisterMethod(targetType, targetMethod, out var _);
                        }
                        EditorGUILayout.LabelField(ReflectionUtils.GetMethodSignature(cachedMethods[i]));
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }

                else EditorGUILayout.LabelField($"No methods found from search: \"{methodSearchStr}\".");
            }

            private void ListRegisteredMethods ()
            {
                var rpcRegistry = target as RPCRegistry;

                // Type methodTypeInRemoval = null;
                RPCMethodInfo ? rpcMethodToRemove = null;

                if (rpcRegistry.currentId > 0)
                {
                    if (targetType != null && targetMethod != null)
                    {
                        EditorGUILayout.LabelField("Selected Method", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"\"{ReflectionUtils.GetMethodSignature(targetMethod)}\" in type: \"{targetType.FullName}\".");
                    }

                    HorizontalLine();
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Registered Methods", EditorStyles.boldLabel);

                    if (GUILayout.Button("Clear", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Unregister ALL Methods?", "Are you sure you to clear ALL registered methods?", "Yes", "Cancel"))
                        {
                            rpcRegistry.Clear();
                            targetMethod = null;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    registeredMethodListPosition = EditorGUILayout.BeginScrollView(registeredMethodListPosition, GUILayout.Height(300));

                    for (ushort i = 0; i < rpcRegistry.currentId; i++)
                    {
                        if (!rpcRegistry.rpcs[i].IsValid)
                            continue;

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            targetMethod = rpcRegistry.rpcs[i].methodInfo;
                            targetType = targetMethod.DeclaringType;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            if (EditorUtility.DisplayDialog("Unregister Method?", "Are you sure you want to unregister this method?", "Yes", "Cancel"))
                                rpcMethodToRemove = rpcRegistry.rpcs[i];
                        }

                        EditorGUILayout.LabelField($"RPC UUID: {rpcRegistry.rpcs[i].id}, Signature: \"{ReflectionUtils.GetMethodSignature(rpcRegistry.rpcs[i].methodInfo)}\"");
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();
                    HorizontalLine();
                }

                else EditorGUILayout.LabelField("No methods reigstered.");

                if (rpcMethodToRemove != null)
                    rpcRegistry.UnregisterMethod(rpcMethodToRemove ?? default(RPCMethodInfo));
            }

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                UpdateTypeSearch(EditorGUILayout.TextField(typeSearchStr));
                HorizontalLine();
                ListTypes();
                HorizontalLine();

                if (targetType != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                        targetType = null;

                    else
                    {
                        EditorGUILayout.LabelField($"Search in: \"{targetType.FullName}\"");
                        EditorGUILayout.EndHorizontal();
                        UpdateMethodSearch(EditorGUILayout.TextField(methodSearchStr));
                        HorizontalLine();
                        ListMethods();
                        HorizontalLine();
                    }
                }

                ListRegisteredMethods();
            }
        }
    }
}
#endif
