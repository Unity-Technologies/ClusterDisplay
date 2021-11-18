using System.IO;
using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    public class ClusterDisplayManagerEditor : EditorWindow
    {
        [MenuItem("Unity/Cluster Display/Cluster Display Manager")]
        private static void Open()
        {
            var window = CreateWindow<ClusterDisplayManagerEditor>();
            window.titleContent = new GUIContent("Cluster Display Manager");
            window.Show();
        }

        private SingletonScriptableObject[] cachedSingletonConfigurables;
        
        private void OnGUI()
        {
            if (cachedSingletonConfigurables == null || cachedSingletonConfigurables.Length == 0)
            {
                string[] paths = AssetDatabase.GetAllAssetPaths();
                cachedSingletonConfigurables = paths
                    .Where(path =>
                        Path.GetExtension(path) == ".asset" &&
                        typeof(IClusterDisplayConfigurable).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path)))
                    .Select(path => AssetDatabase.LoadAssetAtPath<SingletonScriptableObject>(path))
                    .ToArray();
            }

            if (ClusterSync.TryGetInstance(out var clusterSync))
            {
                var editorConfig = clusterSync.EditorConfig;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Is Emitter");
                bool isEmitter = EditorGUILayout.Toggle(editorConfig.m_EditorInstanceIsEmitter);
                if (isEmitter != editorConfig.m_EditorInstanceIsEmitter)
                {
                    editorConfig.m_EditorInstanceIsEmitter = isEmitter;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Emitter CMD");
                string emitterCMD = EditorGUILayout.TextField(editorConfig.m_EditorInstanceEmitterCmdLine);
                if (emitterCMD != editorConfig.m_EditorInstanceEmitterCmdLine)
                {
                    editorConfig.m_EditorInstanceEmitterCmdLine = emitterCMD;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Repeater CMD");
                string repeaterCMD = EditorGUILayout.TextField(editorConfig.m_EditorInstanceRepeaterCmdLine);
                if (repeaterCMD != editorConfig.m_EditorInstanceRepeaterCmdLine)
                {
                    editorConfig.m_EditorInstanceRepeaterCmdLine = repeaterCMD;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Ignore CMD");
                bool ignore = EditorGUILayout.Toggle(editorConfig.m_IgnoreEditorCmdLine);
                if (ignore != editorConfig.m_IgnoreEditorCmdLine)
                {
                    editorConfig.m_IgnoreEditorCmdLine = ignore;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Use Target Framerate");
                bool useTargetFramerate = EditorGUILayout.Toggle(editorConfig.m_UseTargetFramerate);
                if (useTargetFramerate != editorConfig.m_UseTargetFramerate)
                {
                    editorConfig.m_UseTargetFramerate = useTargetFramerate;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target Framerate");
                int targetFrameRate = EditorGUILayout.IntField(editorConfig.m_TargetFrameRate);
                if (targetFrameRate != editorConfig.m_TargetFrameRate)
                {
                    editorConfig.m_TargetFrameRate = targetFrameRate;
                    clusterSync.EditorConfig = editorConfig;
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Play"))
                {
                    editorConfig.SetupForEditorTesting(isEmitter: false);
                    editorConfig.UseEditorCmd(useEditorCmd: false);
                    
                    clusterSync.EditorConfig = editorConfig;
                    EditorApplication.EnterPlaymode();
                }

                if (GUILayout.Button("Play as Emitter"))
                {
                    editorConfig.SetupForEditorTesting(isEmitter: true);
                    editorConfig.UseEditorCmd(useEditorCmd: true);
                    
                    clusterSync.EditorConfig = editorConfig;
                    EditorApplication.EnterPlaymode();
                }

                if (GUILayout.Button("Play as Repeater"))
                {
                    editorConfig.SetupForEditorTesting(isEmitter: false);
                    editorConfig.UseEditorCmd(useEditorCmd: true);
                    
                    clusterSync.EditorConfig = editorConfig;
                    EditorApplication.EnterPlaymode();
                }
            }

            /*
            for (int i = 0; i < cachedSingletonConfigurables.Length; i++)
            {
                SerializedObject serializedObject = new SerializedObject(cachedSingletonConfigurables[i]);
                var editor = UnityEditor.Editor.CreateEditor(cachedSingletonConfigurables[i]);
                editor.OnInspectorGUI();
            }
            */
        }
    }
}
