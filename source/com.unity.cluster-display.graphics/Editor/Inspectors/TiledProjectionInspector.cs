using System;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(TiledProjection))]
    class TiledProjectionInspector : NestedInspector
    {
        SerializedProperty m_GridProp;
        SerializedProperty m_ScreenSizeProp;
        SerializedProperty m_BezelProp;
        SerializedProperty m_IsDebugProp;
        SerializedProperty m_TileIndexProp;
        SerializedProperty m_LayoutProp;
        SerializedProperty m_KeywordProp;
        SerializedProperty m_DebugViewportProp;
        SerializedProperty m_PresentClearColorProp;
        SerializedProperty m_ViewportSectionProp;
        SerializedProperty m_ScaleBiasProp;

        public void OnEnable()
        {
            m_GridProp = serializedObject.FindProperty("m_Settings.GridSize");
            m_ScreenSizeProp = serializedObject.FindProperty("m_Settings.PhysicalScreenSize");
            m_BezelProp = serializedObject.FindProperty("m_Settings.Bezel");
            m_IsDebugProp = serializedObject.FindProperty("m_IsDebug");
            m_TileIndexProp = serializedObject.FindProperty("m_NodeIndexOverride");
            m_LayoutProp = serializedObject.FindProperty("m_DebugSettings.LayoutMode");
            m_KeywordProp = serializedObject.FindProperty("m_DebugSettings.EnableKeyword");
            m_PresentClearColorProp = serializedObject.FindProperty("m_DebugSettings.PresentClearColor");
            m_DebugViewportProp = serializedObject.FindProperty("m_DebugSettings.UseDebugViewportSubsection");
            m_ViewportSectionProp = serializedObject.FindProperty("m_DebugSettings.ViewportSubsection");
            m_ScaleBiasProp = serializedObject.FindProperty("m_DebugSettings.ScaleBiasTextOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_GridProp, Labels.GetGUIContent(Labels.Field.GridSize));
                EditorGUILayout.PropertyField(m_ScreenSizeProp, Labels.GetGUIContent(Labels.Field.PhysicalScreenSize));
                EditorGUILayout.PropertyField(m_BezelProp, Labels.GetGUIContent(Labels.Field.Bezel));
                EditorGUILayout.PropertyField(m_TileIndexProp);
                EditorGUILayout.PropertyField(m_IsDebugProp, Labels.GetGUIContent(Labels.Field.Debug));
                if (m_IsDebugProp.boolValue)
                {
                    EditDebugSettings();
                }

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        void EditDebugSettings()
        {
            EditorGUILayout.PropertyField(m_KeywordProp, Labels.GetGUIContent(Labels.Field.Keyword));
            EditorGUILayout.PropertyField(m_LayoutProp);
            if ((LayoutMode) m_LayoutProp.intValue == LayoutMode.StandardStitcher)
            {
#if CLUSTER_DISPLAY_HDRP
                EditorGUILayout.HelpBox("Standard Stitcher mode does not supportcamera persistent history (used by "+
                    "effects like motion blur), it will be disabled.", MessageType.Warning);
#endif
                EditorGUILayout.PropertyField(m_PresentClearColorProp);
            }

            EditorGUILayout.LabelField(Labels.GetGUIContent(Labels.Field.ScaleBiasOffset));
            var scaleBiasOffset = m_ScaleBiasProp.vector2Value;
            scaleBiasOffset.x = EditorGUILayout.Slider("x", scaleBiasOffset.x, -1, 1);
            scaleBiasOffset.y = EditorGUILayout.Slider("y", scaleBiasOffset.y, -1, 1);
            m_ScaleBiasProp.vector2Value = scaleBiasOffset;

            if ((LayoutMode) m_LayoutProp.intValue == LayoutMode.StandardTile)
            {
                EditorGUILayout.PropertyField(m_DebugViewportProp, Labels.GetGUIContent(Labels.Field.DebugViewportSubsection));
                if (m_DebugViewportProp.boolValue)
                {
                    var viewportRect = m_ViewportSectionProp.rectValue;

                    viewportRect.xMin = EditorGUILayout.Slider("xMin", viewportRect.xMin, 0, 1);
                    viewportRect.xMax = EditorGUILayout.Slider("xMax", viewportRect.xMax, 0, 1);
                    viewportRect.yMin = EditorGUILayout.Slider("yMin", viewportRect.yMin, 0, 1);
                    viewportRect.yMax = EditorGUILayout.Slider("yMax", viewportRect.yMax, 0, 1);
                    m_ViewportSectionProp.rectValue = viewportRect;
                }
            }
        }
    }
}
