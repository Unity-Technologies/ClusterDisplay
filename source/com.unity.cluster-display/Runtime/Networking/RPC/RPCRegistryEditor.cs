﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using UnityEngine.UIElements;
using System.IO;

#if UNITY_EDITOR
using UnityEditorInternal;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>
    {
        [CustomEditor(typeof(RPCRegistry))]
        private class RPCRegistryEditor : Editor
        {
            private Assembly[] cachedAllAssemblies;
            private string assemblySearchStr = "";
            private Vector2 assemblyListScrollPosition;
            private Vector2 registeredAssemblyListScrollPosition;

            private Assembly[] cachedSearchedAssemblies;
            private string[] cachedSearchedAssemblyNames;

            private Assembly selectedAssembly;

            private string typeSearchStr = "";
            private Type[] cachedTypes;
            private Type targetType;
            private Vector2 typeListScrollPosition;

            private string methodSearchStr = "";
            private MethodInfo[] cachedMethods;
            private MethodInfo targetMethod;
            private Vector2 methodListScrollPosition;

            private Vector2 registeredMethodListPosition;

            private ReorderableList assemblyList;

            private void UpdateAssemblySearch (string newAssemblySearchStr, bool forceUpdate = false)
            {
                if (!forceUpdate && newAssemblySearchStr == assemblySearchStr)
                    return;

                if (cachedAllAssemblies == null)
                    cachedAllAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => ReflectionUtils.IsAssemblyPostProcessable(assembly)).ToArray();

                assemblySearchStr = newAssemblySearchStr;
                cachedSearchedAssemblies = cachedAllAssemblies.Where(assembly => string.IsNullOrEmpty(assemblySearchStr) || assembly.FullName.Contains(assemblySearchStr)).ToArray();
                cachedSearchedAssemblyNames = cachedSearchedAssemblies.Select(assembly => assembly.GetName().Name).ToArray();
            }

            private void ListAssemblies ()
            {
                var rpcRegistery = target as RPCRegistry;
                if (cachedSearchedAssemblyNames != null && cachedSearchedAssemblyNames.Length > 0)
                {
                    EditorGUILayout.LabelField("Filtered Assemblies", EditorStyles.boldLabel);
                    assemblyListScrollPosition = EditorGUILayout.BeginScrollView(assemblyListScrollPosition, GUILayout.Height(150));

                    for (int i = 0; i < cachedSearchedAssemblyNames.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            selectedAssembly = cachedSearchedAssemblies[i];
                            UpdateTypeSearch(typeSearchStr, forceUpdate: true);
                        }

                        var assemblyIsRegistered = rpcRegistery.AssemblyIsRegistered(cachedSearchedAssemblies[i]);
                        if (GUILayout.Button(assemblyIsRegistered ? "Unregister" : "Register", GUILayout.Width(60)))
                        {
                            if (assemblyIsRegistered)
                                rpcRegistery.UnregisterAssembly(cachedSearchedAssemblies[i]);
                            else
                                rpcRegistery.RegisterAssembly(cachedSearchedAssemblies[i]);
                        }

                        EditorGUILayout.LabelField(cachedSearchedAssemblyNames[i]);

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();
                }

                else EditorGUILayout.LabelField($"No assemblies found from search: \"{assemblySearchStr}\".");
            }

            private void ListRegisteredAssemblies ()
            {
                if (cachedAllAssemblies == null)
                    return;

                var rpcRegistery = target as RPCRegistry;
                if (RPCRegistry.targetAssemblies != null && RPCRegistry.targetAssemblies.Count > 0)
                {
                    EditorGUILayout.LabelField("Registered Assemblies", EditorStyles.boldLabel);
                    registeredAssemblyListScrollPosition = EditorGUILayout.BeginScrollView(registeredAssemblyListScrollPosition, GUILayout.Height(150));

                    for (int i = 0; i < RPCRegistry.targetAssemblies.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            selectedAssembly = RPCRegistry.targetAssemblies[i];
                            UpdateTypeSearch(typeSearchStr, forceUpdate: true);
                        }

                        if (GUILayout.Button("Unregister", GUILayout.Width(70)))
                            rpcRegistery.UnregisterAssembly(RPCRegistry.targetAssemblies[i]);

                        EditorGUILayout.LabelField(RPCRegistry.targetAssemblies[i].GetName().Name);
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            private void UpdateTypeSearch (string newClassSearchStr, bool forceUpdate = false)
            {
                if (!forceUpdate && newClassSearchStr == typeSearchStr)
                    return;

                if (selectedAssembly == null)
                    return;

                typeSearchStr = newClassSearchStr;

                cachedTypes = ReflectionUtils.GetAllTypes(typeSearchStr, selectedAssembly, includeGenerics: false);
            }

            private void ListTypes ()
            {
                if (cachedTypes == null)
                    UpdateTypeSearch(typeSearchStr, forceUpdate: true);

                if (cachedTypes != null && cachedTypes.Length > 0)
                {
                    EditorGUILayout.LabelField("Filtered Types", EditorStyles.boldLabel);
                    typeListScrollPosition = EditorGUILayout.BeginScrollView(typeListScrollPosition, GUILayout.Height(150));
                    for (int i = 0; i < cachedTypes.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Target", GUILayout.Width(60)))
                        {
                            targetType = cachedTypes[i];
                            OnChangeSearch(methodSearchStr);
                        }

                        EditorGUILayout.LabelField(cachedTypes[i].FullName);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }

                else EditorGUILayout.LabelField($"No types found from search: \"{typeSearchStr}\".");
            }

            private void ListRegisteredMethods ()
            {
                var rpcRegistry = target as RPCRegistry;

                // Type methodTypeInRemoval = null;
                RPCMethodInfo ? rpcMethodToRemove = null;

                if (RPCRegistry.RPCCount > 0)
                {
                    if (targetType != null && targetMethod != null)
                    {
                        EditorGUILayout.LabelField("Selected Method", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"\"{ReflectionUtils.GetMethodSignature(targetMethod)}\" in type: \"{targetType.FullName}\".");
                    }

                    RPCEditorGUICommon.HorizontalLine();
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

                    rpcRegistry.Foreach((Action<RPCMethodInfo>)((rpcMethodInfo) =>
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);

                        if (GUILayout.Button("Select", GUILayout.Width(rpcMethodInfo.IsStatic ? 60 : 90)))
                        {
                            targetMethod = rpcMethodInfo.methodInfo;
                            targetType = targetMethod.DeclaringType;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            if (EditorUtility.DisplayDialog("Unregister Method?", "Are you sure you want to unregister this method?", "Yes", "Cancel"))
                                rpcMethodToRemove = rpcMethodInfo;
                        }

                        EditorGUILayout.LabelField($"RPC UUID: {rpcMethodInfo.rpcId}, Signature: \"{ReflectionUtils.GetMethodSignature(rpcMethodInfo.methodInfo)}\"");
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUILayout.LabelField("Execution Stage:", GUILayout.Width(125));

                        var newRPCExecutionStage = (RPCExecutionStage)EditorGUILayout.EnumPopup(rpcMethodInfo.rpcExecutionStage);
                        if (newRPCExecutionStage != rpcMethodInfo.rpcExecutionStage)
                        {
                            rpcMethodInfo.rpcExecutionStage = newRPCExecutionStage;
                            rpcRegistry.UpdateRPC(ref rpcMethodInfo);
                        }

                        EditorGUILayout.EndHorizontal();
                    }));

                    EditorGUILayout.EndScrollView();
                    RPCEditorGUICommon.HorizontalLine();
                }

                else EditorGUILayout.LabelField("No methods reigstered.");

                if (rpcMethodToRemove != null)
                    RPCRegistry.RemoveRPC(rpcMethodToRemove.Value.rpcId);
            }

            private void OnChangeSearch (string newMethodSearchStr)
            {
                cachedMethods = ReflectionUtils.GetMethodsWithRPCCompatibleParamters(
                    targetType,
                    newMethodSearchStr);

                methodSearchStr = newMethodSearchStr;
            }

            private void OnSelectMethod (MethodInfo selectedMethodInfo)
            {
                RPCRegistry.TryAddNewRPC(
                    targetType, 
                    selectedMethodInfo, 
                    RPCExecutionStage.Automatic, 
                    out var rpcMethodInfo);
            }

            private bool changed = false;
            public override void OnInspectorGUI()
            {
                RPCRegistry rpcRegistry = target as RPCRegistry;

                if (GUILayout.Button("Reset"))
                    rpcRegistry.Clear();

                RPCEditorGUICommon.HorizontalLine();
                EditorGUILayout.LabelField("Assemblies", EditorStyles.boldLabel);
                UpdateAssemblySearch(EditorGUILayout.TextField(assemblySearchStr));
                ListAssemblies();
                ListRegisteredAssemblies();
                RPCEditorGUICommon.HorizontalLine();

                // assemblyList.DoLayoutList();

                EditorGUILayout.LabelField("Types", EditorStyles.boldLabel);
                UpdateTypeSearch(EditorGUILayout.TextField(typeSearchStr));
                ListTypes();
                RPCEditorGUICommon.HorizontalLine();

                if (targetType != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        targetType = null;
                        EditorGUILayout.EndHorizontal();
                    }

                    else
                    {
                        EditorGUILayout.LabelField($"Target Type: \"{targetType.FullName}\"", EditorStyles.boldLabel);
                        EditorGUILayout.EndHorizontal();
                        RPCEditorGUICommon.HorizontalLine();

                        RPCEditorGUICommon.ListMethods(
                            "Static Methods:",
                            cachedMethods,
                            methodSearchStr,
                            ref methodListScrollPosition,
                            OnChangeSearch,
                            OnSelectMethod);

                        RPCEditorGUICommon.HorizontalLine();
                    }
                }

                ListRegisteredMethods();
                /*
                if (changed)
                    serializedObject.ApplyModifiedProperties();
                */
            }

            private void OnEnable()
            {
                UpdateAssemblySearch(assemblySearchStr, forceUpdate: true);
                /*
                assemblyList = new ReorderableList(
                    serializedObject,
                    serializedObject.FindProperty("targetAssemblies"),
                    true, true, true, true);

                assemblyList.drawHeaderCallback = (Rect rect) => 
                {
                    EditorGUI.LabelField(rect, "Assemblies to Post Process");
                };

                assemblyList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var rpcRegistry = target as RPCRegistry;
                    int selectedIndex = Array.IndexOf(cachedAllAssemblies, cachedAllAssemblies.FirstOrDefault(assembly => assembly.FullName == rpcRegistry.targetAssemblies[index]));
                    EditorGUI.Popup(rect, selectedIndex, cachedSearchedAssemblyNames);
                };

                assemblyList.onChangedCallback = (ReorderableList list) =>
                {
                    serializedObject.ApplyModifiedProperties();
                };
                */
            }
        }
    }
}
#endif