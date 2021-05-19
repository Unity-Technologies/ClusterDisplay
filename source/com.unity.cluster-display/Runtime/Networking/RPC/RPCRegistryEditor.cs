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
    public partial class RPCRegistry : SingletonScriptableObject<RPCRegistry>
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

            private void UpdateTypeSearch (string newClassSearchStr, bool forceUpdate = false)
            {
                if (!forceUpdate && newClassSearchStr == typeSearchStr)
                    return;

                typeSearchStr = newClassSearchStr;

                if (!ReflectionUtils.TryGetDefaultAssembly(out var defaultUserAssembly))
                {
                    Debug.LogError($"Unable to find default user assembly with name: \"{ReflectionUtils.DefaultUserAssemblyName}\".");
                    return;
                }

                cachedTypes = ReflectionUtils.GetAllTypes(typeSearchStr, defaultUserAssembly, includeGenerics: false);
            }

            private void ListTypes ()
            {
                if (cachedTypes == null)
                    UpdateTypeSearch(typeSearchStr, forceUpdate: true);

                if (cachedTypes != null && cachedTypes.Length > 0)
                {
                    EditorGUILayout.LabelField("Types", EditorStyles.boldLabel);
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

                if (rpcRegistry.RPCCount > 0)
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

                    for (ushort i = 0; i < rpcRegistry.RPCUpperBoundID; i++)
                    {
                        var rpcMethodInfo = rpcRegistry.GetRPCByIndex(i);
                        if (!rpcMethodInfo.IsValid)
                            continue;

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(30);

                        if (GUILayout.Button("Select", GUILayout.Width(rpcMethodInfo.IsStatic ? 60 : 90 )))
                        {
                            targetMethod = rpcMethodInfo.methodInfo;
                            targetType = targetMethod.DeclaringType;
                        }

                        if (rpcMethodInfo.IsStatic && GUILayout.Button("X", GUILayout.Width(25)))
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
                            rpcRegistry.SetRPCByIndex(i, rpcMethodInfo);
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();
                    RPCEditorGUICommon.HorizontalLine();
                }

                else EditorGUILayout.LabelField("No methods reigstered.");

                if (rpcMethodToRemove != null)
                    rpcRegistry.DeincrementMethodReference(rpcMethodToRemove.Value.rpcId);
            }

            private void OnChangeSearch (string newMethodSearchStr)
            {
                cachedMethods = ReflectionUtils.GetAllMethodsFromType(
                    targetType, 
                    newMethodSearchStr, 
                    valueTypeParametersOnly: true,
                    bindingFlags: BindingFlags.Public | BindingFlags.Static, 
                    includeGenerics: false);

                methodSearchStr = newMethodSearchStr;
            }

            private void OnSelectMethod (MethodInfo selectedMethodInfo)
            {
                var rpcRegistry = target as RPCRegistry;
                rpcRegistry.TryIncrementMethodReference(targetType, selectedMethodInfo, out var rpcMethodInfo);
            }

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                if (GUILayout.Button("Reset"))
                    (target as RPCRegistry).Clear();

                UpdateTypeSearch(EditorGUILayout.TextField(typeSearchStr));
                RPCEditorGUICommon.HorizontalLine();
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
            }
        }
    }
}
#endif
