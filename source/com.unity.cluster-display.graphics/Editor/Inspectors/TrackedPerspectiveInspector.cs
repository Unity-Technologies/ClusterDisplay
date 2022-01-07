using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomEditor(typeof(TrackedPerspectiveProjection))]
    class TrackedPerspectiveInspector : NestedInspector
    {
        SerializedProperty m_DebugProp;
        SerializedProperty m_SurfacesProp;
        SerializedProperty m_NodeIndexProp;

        ReorderableList m_SurfacesList;

        static readonly Color k_SelectedColor = new Color(235, 116, 52, 255) / 255f;
        static readonly Color k_UnselectedColor = Color.green;
        const int k_LineWidth = 4;
        const string k_UndoCreateSurface = "Create projection surface";
        const string k_UndoDeleteSurface = "Delete projection surface";

        int m_SelectedSurfaceIndex = -1;
        GenericMenu m_NewSurfaceMenu;

        void OnEnable()
        {
            m_DebugProp = serializedObject.FindProperty("m_IsDebug");
            m_SurfacesProp = serializedObject.FindProperty("m_ProjectionSurfaces");
            m_NodeIndexProp = serializedObject.FindProperty("m_NodeIndexOverride");

            m_SurfacesList = new ReorderableList(
                serializedObject,
                m_SurfacesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, Labels.GetGUIContent(Labels.Field.ProjectionSurfaces)),
                drawElementCallback = DrawSurfaceElement,
                onAddDropdownCallback = DisplayAddSurfaceDropdown,
                onRemoveCallback = OnRemoveSurfaceElement,
                elementHeightCallback = index =>
                    ProjectionSurfacePropertyDrawer.GetHeight(m_SurfacesProp.GetArrayElementAtIndex(index))
            };
        }

        public override void OnSceneGUI()
        {
            if (target as TrackedPerspectiveProjection is not { } projection)
            {
                return;
            }

            serializedObject.Update();

            if (m_SelectedSurfaceIndex >= m_SurfacesProp.arraySize)
            {
                m_SelectedSurfaceIndex = -1;
            }

            for (int i = 0; i < m_SurfacesProp.arraySize; i++)
            {
                var currentSurface = projection.Surfaces[i];
                if (i == m_SelectedSurfaceIndex)
                {
                    var newSurface = DoSurfaceHandles(currentSurface, projection.Origin);
                    if (newSurface != currentSurface)
                    {
                        Undo.RecordObject(target, "Modify Projection Surface");
                        projection.SetSurface(m_SelectedSurfaceIndex, newSurface);

                        // We need to update the cluster rendering, but Update and LateUpdate
                        // do not happen after OnSceneGUI changes, so we need to explicitly request
                        // that the Editor execute an update loop.
                        if (!Application.isPlaying)
                        {
                            EditorApplication.QueuePlayerLoopUpdate();
                        }
                    }

                    Handles.color = k_SelectedColor;
                    Handles.DrawAAPolyLine(k_LineWidth, newSurface.GetPolyLine(projection.Origin));
                }
                else
                {
                    Handles.color = k_UnselectedColor;
                    Handles.DrawAAPolyLine(k_LineWidth, currentSurface.GetPolyLine(projection.Origin));
                }
            }
        }

        static ProjectionSurface DoSurfaceHandles(ProjectionSurface surface, Matrix4x4 rootTransform)
        {
            var rotation = rootTransform.rotation * surface.LocalRotation;
            rotation.Normalize();
            var position = rootTransform.MultiplyPoint(surface.LocalPosition);
            rotation = Handles.RotationHandle(rotation, position);
            surface.LocalRotation = (rotation * rootTransform.inverse.rotation).normalized;
            surface.LocalPosition = rootTransform.inverse.MultiplyPoint(Handles.PositionHandle(position, rotation));
            return surface;
        }

        void OnRemoveSurfaceElement(ReorderableList list)
        {
            if (m_SelectedSurfaceIndex >= 0 &&
                target as TrackedPerspectiveProjection is { } projection)
            {
                Undo.RegisterCompleteObjectUndo(target, k_UndoDeleteSurface);
                projection.RemoveSurface(m_SelectedSurfaceIndex);
            }
        }

        void DrawSurfaceElement(Rect rect, int index, bool active, bool focused)
        {
            if (active && focused)
            {
                m_SelectedSurfaceIndex = index;
            }

            var element = m_SurfacesProp.GetArrayElementAtIndex(index);

            EditorGUI.PropertyField(rect, element);
        }

        void DisplayAddSurfaceDropdown(Rect buttonRect, ReorderableList list)
        {
            serializedObject.Update();

            if (m_NewSurfaceMenu == null)
            {
                m_NewSurfaceMenu = new GenericMenu();
                m_NewSurfaceMenu.AddItem(
                    Labels.GetGUIContent(Labels.Field.DefaultProjectionSurface),
                    false,
                    AddDefaultSurface);
            }

            m_NewSurfaceMenu.ShowAsContext();
        }

        void AddDefaultSurface()
        {
            if (target as TrackedPerspectiveProjection is { } projection)
            {
                Undo.RegisterCompleteObjectUndo(target, k_UndoCreateSurface);
                projection.AddSurface();
            }

            if (m_SelectedSurfaceIndex >= 0)
            {
                serializedObject.Update();
                m_SurfacesList.Select(m_SurfacesList.count - 1);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_DebugProp, Labels.GetGUIContent(Labels.Field.Debug));
                m_SurfacesList.DoLayoutList();

                EditorGUILayout.PropertyField(m_NodeIndexProp, Labels.GetGUIContent(Labels.Field.NodeIndexOverride));

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
