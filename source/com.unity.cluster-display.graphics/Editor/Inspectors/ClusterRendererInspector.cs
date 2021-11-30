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
                settings.GridSize = EditorGUILayout.Vector2IntField(Labels.GetGUIContent(Labels.Field.GridSize), settings.GridSize);
                settings.PhysicalScreenSize = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.PhysicalScreenSize), settings.PhysicalScreenSize);
                settings.Bezel = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.Bezel), settings.Bezel);
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
            var settings = clusterRenderer.Settings;
            
            debugSettings.TileIndexOverride = EditorGUILayout.IntField(Labels.GetGUIContent(Labels.Field.TileIndexOverride), debugSettings.TileIndexOverride);

            var prevEnableKeyword = debugSettings.EnableKeyword;
            debugSettings.EnableKeyword = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Keyword), prevEnableKeyword);
            if (debugSettings.EnableKeyword != prevEnableKeyword)
            {
                ClusterRenderer.EnableScreenCoordOverrideKeyword(debugSettings.EnableKeyword);
            }

            debugSettings.LayoutMode = (LayoutMode)EditorGUILayout.EnumPopup(Labels.GetGUIContent(Labels.Field.LayoutMode), debugSettings.LayoutMode);

            debugSettings.UseDebugViewportSubsection = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.DebugViewportSubsection), debugSettings.UseDebugViewportSubsection);

            if (debugSettings.LayoutMode == LayoutMode.StandardStitcher)
            {
                debugSettings.BezelColor = EditorGUILayout.ColorField(Labels.GetGUIContent(Labels.Field.BezelColor), debugSettings.BezelColor);
            }

            // Let user manipulate viewport directly instead of inferring it from tile index.
            if (debugSettings.UseDebugViewportSubsection)
            {
                EditorGUILayout.LabelField("Viewport Section");

                var rect = debugSettings.ViewportSubsection;
                var xMin = rect.xMin;
                var xMax = rect.xMax;
                var yMin = rect.yMin;
                var yMax = rect.yMax;

                xMin = EditorGUILayout.Slider("xMin", xMin, 0, 1);
                xMax = EditorGUILayout.Slider("xMax", xMax, 0, 1);
                yMin = EditorGUILayout.Slider("yMin", yMin, 0, 1);
                yMax = EditorGUILayout.Slider("yMax", yMax, 0, 1);
                debugSettings.ViewportSubsection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
            else
            {
                // Reset viewport subsection.
                debugSettings.ViewportSubsection = Viewport.TileIndexToSubSection(settings.GridSize, debugSettings.TileIndexOverride);
            }

            EditorGUILayout.LabelField(Labels.GetGUIContent(Labels.Field.ScaleBiasOffset));
            var offset = debugSettings.ScaleBiasTextOffset;
            offset.x = EditorGUILayout.Slider("x", offset.x, -1, 1);
            offset.y = EditorGUILayout.Slider("y", offset.y, -1, 1);
            debugSettings.ScaleBiasTextOffset = offset;
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
