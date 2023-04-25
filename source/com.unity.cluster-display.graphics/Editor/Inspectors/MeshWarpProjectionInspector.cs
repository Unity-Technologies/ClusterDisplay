using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using File = UnityEngine.Windows.File;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(MeshWarpProjection))]
    class MeshWarpProjectionInspector : NestedInspector
    {
        static class Contents
        {
            public static readonly GUIContent InvalidMeshIcon =
                EditorGUIUtility.TrIconContent("console.warnicon.sml", "No mesh target selected");

            public static readonly GUIContent InvalidResolutionIcon =
                EditorGUIUtility.TrIconContent("console.warnicon.sml", "Invalid resolution");

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
            public static readonly GUIContent StaticCubemapSnapshot = EditorGUIUtility.TrTextContent(
                "Generate Static Cubemap",
                "Take a snapshot of the realtime cubemap, save it as an asset and use it as the static cubemap");
            public static readonly GUIContent OuterViewPosition = EditorGUIUtility.TrTextContent("Outer View Origin",
                "The position, specified locally, from which to render the outer frustum cubemap.");
            public static readonly GUIContent CubemapSize = EditorGUIUtility.TrTextContent("Cubemap Size",
                "Size of the cubemap in pixels");
            public static readonly int[] CubemapSizes = new[] {1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096};
            public static readonly GUIContent[] CubemapSizesGUI =
                CubemapSizes.Select(s => new GUIContent(s.ToString())).ToArray();
            public const string UndoCreateSurface = "Create projection surface";
            public static readonly GUIContent RotateUVs = EditorGUIUtility.TrTextContent("Rotate mesh UVs");
        }

        SerializedProperty m_IsDebugProp;
        SerializedProperty m_NodeIndexProp;
        SerializedProperty m_SurfacesProp;
        SerializedProperty m_OuterViewPositionProp;
        SerializedProperty m_CubemapSizeProp;
        SerializedProperty m_BackgroundColorProp;
        SerializedProperty m_StaticCubemapProp;
        SerializedProperty m_RenderInnerOuterProp;
        SerializedProperty m_OuterFrustumModeProp;

        ReorderableList m_SurfacesList;

        MeshWarpProjection m_Projection;
        readonly List<GameObject> m_SelectedRenderers = new();

        void OnEnable()
        {
            m_Projection = (MeshWarpProjection) target;
            m_IsDebugProp = serializedObject.FindProperty("m_IsDebug");
            m_NodeIndexProp = serializedObject.FindProperty("m_NodeIndexOverride");
            m_SurfacesProp = serializedObject.FindProperty("m_ProjectionSurfaces");
            m_OuterViewPositionProp = serializedObject.FindProperty("m_OuterViewPosition");
            m_CubemapSizeProp = serializedObject.FindProperty("m_OuterFrustumCubemapSize");
            m_BackgroundColorProp = serializedObject.FindProperty("m_BackgroundColor");
            m_StaticCubemapProp = serializedObject.FindProperty("m_StaticCubemap");
            m_RenderInnerOuterProp = serializedObject.FindProperty("m_RenderInnerOuterFrustum");
            m_OuterFrustumModeProp = serializedObject.FindProperty("m_OuterFrustumMode");

            m_SurfacesList = new ReorderableList(serializedObject, m_SurfacesProp)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, Labels.GetGUIContent(Labels.Field.ProjectionSurfaces)),
                onAddCallback = OnAddSurface,
                drawElementCallback = DrawElement,
                elementHeightCallback = index => (EditorGUIUtility.singleLineHeight + 2) * 3 + 2
            };
        }

        void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
            var element = m_SurfacesProp.GetArrayElementAtIndex(index);
            var position = rect;
            position.width -= 18;
            position.y += 2;
            position.height = lineHeight;
            var meshProp = element.FindPropertyRelative("m_MeshRenderer");
            var resolutionProp = element.FindPropertyRelative("m_ScreenResolution");
            var rotationProp = element.FindPropertyRelative("m_UVRotation");
            EditorGUI.PropertyField(position, meshProp);
            if (meshProp.objectReferenceValue == null)
            {
                var iconPosition = position;
                iconPosition.x = rect.xMax - 16;
                iconPosition.width = 16;
                EditorGUI.LabelField(iconPosition, Contents.InvalidMeshIcon);
            }
            position.y += lineHeight + 2;
            if (!resolutionProp.vector2IntValue.IsValidResolution())
            {
                var iconPosition = position;
                iconPosition.x = rect.xMax - 16;
                iconPosition.width = 16;
                EditorGUI.LabelField(iconPosition, Contents.InvalidResolutionIcon);
            }

            EditorGUI.PropertyField(position, resolutionProp);

            position.y += lineHeight + 2;
            EditorGUI.PropertyField(position, rotationProp, Contents.RotateUVs);
        }

        void OnAddSurface(ReorderableList list)
        {
            Undo.RegisterCompleteObjectUndo(m_Projection, Contents.UndoCreateSurface);
            m_Projection.AddSurface();
        }

        public override void OnSceneGUI()
        {
            // Draw outlines around the projection surfaces (only works during the Repaint event).
            if (Event.current.type != EventType.Repaint) return;

            m_SelectedRenderers.Clear();
            if (m_SurfacesList.selectedIndices.Count == 0)
            {
                foreach (var surface in m_Projection.ProjectionSurfaces)
                {
                    if (surface.MeshRenderer != null)
                    {
                        m_SelectedRenderers.Add(surface.MeshRenderer.gameObject);
                    }
                }
            }
            else
            {
                foreach (var index in m_SurfacesList.selectedIndices)
                {
                    if (index < 0 || index >= m_Projection.ProjectionSurfaces.Count) break;

                    var renderer = m_Projection.ProjectionSurfaces[index].MeshRenderer;
                    if (renderer != null)
                    {
                        m_SelectedRenderers.Add(renderer.gameObject);
                    }
                }
            }
            Handles.DrawOutline(m_SelectedRenderers, Color.green);
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
                switch ((MeshWarpProjection.OuterFrustumMode)m_OuterFrustumModeProp.enumValueIndex)
                {
                    case MeshWarpProjection.OuterFrustumMode.SolidColor:
                        EditorGUILayout.PropertyField(m_BackgroundColorProp, Contents.BackgroundColor);
                        break;
                    case MeshWarpProjection.OuterFrustumMode.StaticCubemap:
                        EditorGUILayout.PropertyField(m_StaticCubemapProp, Contents.StaticCubemap);
                        if (GUILayout.Button(Contents.StaticCubemapSnapshot))
                        {
                            m_Projection.OnPostRenderRealtimeCubemap += PostRealtimeCubemapRender;
                            m_Projection.OuterFrustumModeEditorAccess =
                                MeshWarpProjection.OuterFrustumMode.RealtimeCubemap;

                            // The two lines below is to be sure that something will draw the scene.  The easiest way
                            // is to make the scene view visible and force a repaint.
                            EditorWindow.GetWindow(typeof(SceneView));
                            SceneView.RepaintAll();
                        }
                        break;
                    case MeshWarpProjection.OuterFrustumMode.RealtimeCubemap:
                        EditorGUILayout.PropertyField(m_OuterViewPositionProp, Contents.OuterViewPosition);
                        m_CubemapSizeProp.intValue = EditorGUILayout.IntPopup(Contents.CubemapSize,
                            m_CubemapSizeProp.intValue, Contents.CubemapSizesGUI, Contents.CubemapSizes);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }


            // EditorGUILayout.PropertyField(m_SurfacesProp);
            m_SurfacesList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        void PostRealtimeCubemapRender(RenderTexture cubemap)
        {
            // Unhook this callback (we only want it to be called once)
            m_Projection.OuterFrustumModeEditorAccess = MeshWarpProjection.OuterFrustumMode.StaticCubemap;
            m_Projection.OnPostRenderRealtimeCubemap -= PostRealtimeCubemapRender;

            // Create the cubemap asset
            var cubemapAsset = GraphicsUtil.RenderTextureCubemapToCubemap(cubemap);
            string snapshotFolder = "MeshWarpStaticCubemap";
            if (!AssetDatabase.IsValidFolder($"Assets/{snapshotFolder}"))
            {
                AssetDatabase.CreateFolder("Assets", snapshotFolder);
            }
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/{snapshotFolder}/snapshot.asset");
            AssetDatabase.CreateAsset(cubemapAsset, assetPath);
            AssetDatabase.SaveAssets();

            // Set the projection properties
            Undo.RecordObject(m_Projection, "Snapshot Realtime Cubemap");
            m_Projection.StaticCubeMapEditorAccess = cubemapAsset;

            Undo.SetCurrentGroupName("Take snapshot of realtime cubemap");
        }
    }
}
