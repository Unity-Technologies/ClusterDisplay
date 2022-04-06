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
        [MenuItem("Cluster Display/Cluster Display Manager")]
        private static void Open()
        {
            var window = CreateWindow<ClusterDisplayManagerEditor>();
            window.titleContent = new GUIContent("Cluster Display Manager");
            window.Show();
        }

        private SingletonScriptableObject[] cachedSingletonConfigurables;
        
        private void OnGUI()
        {
            if (!ClusterSyncEditorConfig.TryGetInstance(out var editorConfig))
            {
                return;
            }

            bool modified = false;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Is Emitter");
            bool isEmitter = EditorGUILayout.Toggle(editorConfig.m_EditorInstanceIsEmitter);
            if (isEmitter != editorConfig.m_EditorInstanceIsEmitter)
            {
                editorConfig.m_EditorInstanceIsEmitter = isEmitter;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Emitter CMD");
            string emitterCMD = EditorGUILayout.TextField(editorConfig.m_EditorInstanceEmitterCmdLine);
            if (emitterCMD != editorConfig.m_EditorInstanceEmitterCmdLine)
            {
                editorConfig.m_EditorInstanceEmitterCmdLine = emitterCMD;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Repeater CMD");
            string repeaterCMD = EditorGUILayout.TextField(editorConfig.m_EditorInstanceRepeaterCmdLine);
            if (repeaterCMD != editorConfig.m_EditorInstanceRepeaterCmdLine)
            {
                editorConfig.m_EditorInstanceRepeaterCmdLine = repeaterCMD;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ignore CMD");
            bool ignore = EditorGUILayout.Toggle(editorConfig.m_IgnoreEditorCmdLine);
            if (ignore != editorConfig.m_IgnoreEditorCmdLine)
            {
                editorConfig.m_IgnoreEditorCmdLine = ignore;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Use Target Framerate");
            bool useTargetFramerate = EditorGUILayout.Toggle(editorConfig.m_UseTargetFramerate);
            if (useTargetFramerate != editorConfig.m_UseTargetFramerate)
            {
                editorConfig.m_UseTargetFramerate = useTargetFramerate;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Framerate");
            int targetFrameRate = EditorGUILayout.IntField(editorConfig.m_TargetFrameRate);
            if (targetFrameRate != editorConfig.m_TargetFrameRate)
            {
                editorConfig.m_TargetFrameRate = targetFrameRate;
                modified = true;
            }
            EditorGUILayout.EndHorizontal();

            if (modified)
            {
                EditorUtility.SetDirty(editorConfig);
            }

            if (GUILayout.Button("Play"))
            {
                editorConfig.SetupForEditorTesting(isEmitter: false);
                editorConfig.UseEditorCmd(useEditorCmd: false);
                
                EditorApplication.EnterPlaymode();
            }

            if (GUILayout.Button("Play as Emitter"))
            {
                editorConfig.SetupForEditorTesting(isEmitter: true);
                editorConfig.UseEditorCmd(useEditorCmd: true);
                
                EditorApplication.EnterPlaymode();
            }

            if (GUILayout.Button("Play as Repeater"))
            {
                editorConfig.SetupForEditorTesting(isEmitter: false);
                editorConfig.UseEditorCmd(useEditorCmd: true);
                
                EditorApplication.EnterPlaymode();
            }
        }
    }
}
