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
        SerializedProperty m_EnableGUIProp;
        
        void OnEnable()
        {
            m_EnableGUIProp = serializedObject.FindProperty("m_EnableGUI");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
#if CLUSTER_DISPLAY_URP
                RenderFeatureEditorUtils<ClusterRenderer, InjectionPointRenderFeature>.OnInspectorGUI();
#endif
                CheckForClusterCameraComponents();
                
                // TODO GUI Content
                EditorGUILayout.PropertyField(m_EnableGUIProp);
                
                var adapter = target as ClusterRenderer;

                var settings = adapter.Settings;
                settings.GridSize = EditorGUILayout.Vector2IntField(Labels.GetGUIContent(Labels.Field.GridSize), settings.GridSize);
                settings.PhysicalScreenSize = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.PhysicalScreenSize), settings.PhysicalScreenSize);
                settings.Bezel = EditorGUILayout.Vector2Field(Labels.GetGUIContent(Labels.Field.Bezel), settings.Bezel);
                settings.OverScanInPixels = EditorGUILayout.IntSlider(Labels.GetGUIContent(Labels.Field.Overscan), settings.OverScanInPixels, 0, 256);

                adapter.Context.Debug = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Debug), adapter.Context.Debug);

                if (adapter.Context.Debug)
                {
                    EditDebugSettings(adapter.debugSettings, adapter.Context, adapter);
                }

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    
                    // TODO needed?
                    EditorUtility.SetDirty(adapter);
                }
            }
        }

        // TODO renderer exposes both debug-settings and context, redundant arguments.
        static void EditDebugSettings(ClusterRendererDebugSettings settings, ClusterRenderContext context, ClusterRenderer renderer)
        {
            settings.TileIndexOverride = EditorGUILayout.IntField(Labels.GetGUIContent(Labels.Field.TileIndexOverride), settings.TileIndexOverride);

            var prevEnableKeyword = settings.EnableKeyword;
            settings.EnableKeyword = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.Keyword), prevEnableKeyword);
            if (settings.EnableKeyword != prevEnableKeyword)
            {
                GraphicsUtil.SetShaderKeyword(settings.EnableKeyword);
            }

            var prevLayoutMode = settings.LayoutMode;
            settings.LayoutMode = (LayoutMode)EditorGUILayout.EnumPopup(Labels.GetGUIContent(Labels.Field.LayoutMode), prevLayoutMode);
            if (settings.LayoutMode != prevLayoutMode)
            {
                renderer.SetLayoutMode(settings.LayoutMode);
            }
            
            settings.UseDebugViewportSubsection = EditorGUILayout.Toggle(Labels.GetGUIContent(Labels.Field.DebugViewportSubsection), settings.UseDebugViewportSubsection);

            if (settings.LayoutMode == LayoutMode.StandardStitcher)
            {
                settings.BezelColor = EditorGUILayout.ColorField(Labels.GetGUIContent(Labels.Field.BezelColor), settings.BezelColor);
            }

            // Let user manipulate viewport directly instead of inferring it from tile index.
            if (settings.UseDebugViewportSubsection)
            {
                EditorGUILayout.LabelField("Viewport Section");

                var rect = settings.ViewportSubsection;
                var xMin = rect.xMin;
                var xMax = rect.xMax;
                var yMin = rect.yMin;
                var yMax = rect.yMax;

                xMin = EditorGUILayout.Slider("xMin", xMin, 0, 1);
                xMax = EditorGUILayout.Slider("xMax", xMax, 0, 1);
                yMin = EditorGUILayout.Slider("yMin", yMin, 0, 1);
                yMax = EditorGUILayout.Slider("yMax", yMax, 0, 1);
                settings.ViewportSubsection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
            else
            {
                // Reset viewport subsection.
                settings.ViewportSubsection = GraphicsUtil.TileIndexToViewportSection(context.GridSize, context.TileIndex);
            }

            EditorGUILayout.LabelField(Labels.GetGUIContent(Labels.Field.ScaleBiasOffset));
            var offset = settings.ScaleBiasTextOffset;
            offset.x = EditorGUILayout.Slider("x", offset.x, -1, 1);
            offset.y = EditorGUILayout.Slider("y", offset.y, -1, 1);
            settings.ScaleBiasTextOffset = offset;
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
