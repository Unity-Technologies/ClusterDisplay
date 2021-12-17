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

        const string k_UndoCreateSurface = "Create projection surface";
        const string k_UndoDeleteSurface = "Delete projection surface";

        int m_SelectedSurfaceIndex = -1;

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

            foreach (var surface in projection.Surfaces)
            {
                Handles.DrawLines(surface.GetVertices(projection.Origin), surface.DrawOrder);
            }

            if (m_SelectedSurfaceIndex >= m_SurfacesProp.arraySize)
            {
                m_SelectedSurfaceIndex = -1;
            }
            
            if (m_SelectedSurfaceIndex >= 0)
            {
                var currentSurface = projection.Surfaces[m_SelectedSurfaceIndex];
                var newSurface = DoSurfaceHandles(currentSurface, projection.Origin);
                if (newSurface != currentSurface)
                {
                    Undo.RecordObject(target, "Modify Projection Surface");
                    projection.SetSurface(m_SelectedSurfaceIndex, newSurface);
                    
                    // We need to update the cluster rendering, but Update and LateUpdate
                    // do not happen after OnSceneGUI changes, so we need to explicitly request
                    // an updated.
                    EditorApplication.QueuePlayerLoopUpdate();
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
            var menu = new GenericMenu();

            menu.AddItem(Labels.GetGUIContent(Labels.Field.DefaultProjectionSurface), false, AddDefaultSurface);
            menu.ShowAsContext();
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
                }
            }
        }
    }
}
