using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(ClusterRenderer))]
    class ClusterRendererInspector : UnityEditor.Editor
    {
        const string k_NoCamerasMessage = "No cameras are marked to render in this cluster.";
        const string k_AddCameraScriptText = "Add ClusterCamera component to all cameras";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
#if CLUSTER_DISPLAY_URP
                RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();
#endif
                CheckForClusterCameraComponents();
                
                var clusterRenderer = target as ClusterRenderer;

                var settings = clusterRenderer.Settings;
                settings.OverScanInPixels = EditorGUILayout.IntSlider(Labels.GetGUIContent(Labels.Field.Overscan), settings.OverScanInPixels, 0, 256);

                clusterRenderer.IsDebug = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Debug), clusterRenderer.IsDebug);

                if (clusterRenderer.IsDebug)
                {
                    EditDebugSettings(clusterRenderer);
                }

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    
                    // TODO needed?
                    EditorUtility.SetDirty(clusterRenderer);
                }
            }
        }

        // TODO renderer exposes both debug-settings and context, redundant arguments.
        static void EditDebugSettings(ClusterRenderer clusterRenderer)
        {
            var debugSettings = clusterRenderer.DebugSettings;
            
            var prevEnableKeyword = debugSettings.EnableKeyword;
            debugSettings.EnableKeyword = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Keyword), prevEnableKeyword);
            if (debugSettings.EnableKeyword != prevEnableKeyword)
            {
                GraphicsUtil.SetShaderKeyword(debugSettings.EnableKeyword);
            }
        }

        static void CheckForClusterCameraComponents()
        {
            if (!SceneUtils.FindAllObjectsInScene<ClusterCamera>().Any())
            {
                EditorGUILayout.HelpBox(k_NoCamerasMessage, MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(k_AddCameraScriptText))
                    {
                        AddMissingClusterCameraComponents();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        static void AddMissingClusterCameraComponents()
        {
            foreach (var camera in SceneUtils.FindAllObjectsInScene<Camera>())
            {
                if (camera.GetComponent<ClusterRenderer>() == null && camera.GetComponent<ClusterCamera>() == null)
                {
                    camera.gameObject.AddComponent<ClusterCamera>();
                    Debug.Log($"Added {typeof(ClusterCamera)} component to {camera.name}");
                }
            }
        }
    }
}
