using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(MeshWarpProjection))]
    class MeshWarpInspector : NestedInspector
    {
        static class Contents
        {
            public static readonly GUIStyle ButtonToggleStyle = "Button";
            public static readonly GUIContent IsDebug = EditorGUIUtility.TrTextContent("Debug Mode",
                "Preview meshes in Editor.");
            public static readonly GUIContent NodeIndex = EditorGUIUtility.TrTextContent("Node Index Override",
                "Render the specified node (when previewing in the Editor");

            public static readonly GUIContent InnerOuterFrustum = EditorGUIUtility.TrTextContent(
                "Inner/Outer Frustum",
                "Render separate inner and outer frustum regions. The active camera is reflected in the inner frustum.");

            public static readonly GUIContent FullScreen = EditorGUIUtility.TrTextContent(
                "Full Screen",
                "Fill the entire mesh surface with the projection.");

            public static readonly GUIContent OuterFrustumMode = EditorGUIUtility.TrTextContent("Outer Frustum Mode",
                "How the outer frustum region is rendered.");
            public static readonly GUIContent BackgroundColor = EditorGUIUtility.TrTextContent("Background Color");
            public static readonly GUIContent StaticCubemap = EditorGUIUtility.TrTextContent("Cubemap");
            public static readonly GUIContent OuterViewPosition = EditorGUIUtility.TrTextContent("Outer View Origin",
                "The position, specified locally, from which to render the outer frustum cubemap.");
            public static readonly GUIContent CubemapSize = EditorGUIUtility.TrTextContent("Cubemap Size",
                "Size of the cubemap in pixels");
        }

        SerializedProperty m_IsDebugProp;
        SerializedProperty m_NodeIndexProp;
        SerializedProperty m_MeshesProp;
        SerializedProperty m_OuterViewPositionProp;
        SerializedProperty m_CubemapSizeProp;
        SerializedProperty m_BackgroundColorProp;
        SerializedProperty m_StaticCubemapProp;
        SerializedProperty m_RenderInnerOuterProp;
        SerializedProperty m_OuterFrustumModeProp;

        void OnEnable()
        {
            m_IsDebugProp = serializedObject.FindProperty("m_IsDebug");
            m_NodeIndexProp = serializedObject.FindProperty("m_NodeIndexOverride");
            m_MeshesProp = serializedObject.FindProperty("m_Meshes");
            m_OuterViewPositionProp = serializedObject.FindProperty("m_OuterViewPosition");
            m_CubemapSizeProp = serializedObject.FindProperty("m_OuterFrustumCubemapSize");
            m_BackgroundColorProp = serializedObject.FindProperty("m_BackgroundColor");
            m_StaticCubemapProp = serializedObject.FindProperty("m_StaticCubemap");
            m_RenderInnerOuterProp = serializedObject.FindProperty("m_RenderInnerOuterFrustum");
            m_OuterFrustumModeProp = serializedObject.FindProperty("m_OuterFrustumMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_IsDebugProp, Contents.IsDebug);
            EditorGUILayout.PropertyField(m_NodeIndexProp, Contents.NodeIndex);

            var outerFrustumEnabled = m_RenderInnerOuterProp.boolValue;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    outerFrustumEnabled = GUILayout.Toggle(outerFrustumEnabled, Contents.InnerOuterFrustum, Contents.ButtonToggleStyle);
                    outerFrustumEnabled = !GUILayout.Toggle(!outerFrustumEnabled, Contents.FullScreen, Contents.ButtonToggleStyle);

                    if (change.changed)
                    {
                        m_RenderInnerOuterProp.boolValue = outerFrustumEnabled;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            if (outerFrustumEnabled)
            {
                EditorGUILayout.PropertyField(m_OuterFrustumModeProp, Contents.OuterFrustumMode);
                switch (m_OuterFrustumModeProp.enumValueIndex)
                {
                    case (int) MeshWarpProjection.OuterFrustumMode.SolidColor:
                        EditorGUILayout.PropertyField(m_BackgroundColorProp, Contents.BackgroundColor);
                        break;
                    case (int) MeshWarpProjection.OuterFrustumMode.StaticCubemap:
                        EditorGUILayout.PropertyField(m_StaticCubemapProp, Contents.StaticCubemap);
                        break;
                    case (int)MeshWarpProjection.OuterFrustumMode.RealtimeCubemap:
                        EditorGUILayout.PropertyField(m_OuterViewPositionProp, Contents.OuterViewPosition);
                        EditorGUILayout.PropertyField(m_CubemapSizeProp, Contents.CubemapSize);
                        break;
                }
            }

            EditorGUILayout.PropertyField(m_MeshesProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
