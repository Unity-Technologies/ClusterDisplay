using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

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

        TrackedPerspectiveSurface m_SelectedSurface;

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
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, Labels.GetGUIContent(Labels.Field.ProjectionSurfaces)),
                drawElementCallback = OnDrawSurfaceElement,
                onAddDropdownCallback = DisplayAddSurfaceDropdown,
                onSelectCallback = OnSelectSurfaceElement,
                onRemoveCallback = OnRemoveSurfaceElement
            };
        }

        void OnSelectSurfaceElement(ReorderableList list)
        {
            m_SelectedSurface = m_SurfacesProp.GetArrayElementAtIndex(list.index).managedReferenceValue as TrackedPerspectiveSurface;
        }

        public override void OnSceneGUI()
        {
            if (target as TrackedPerspectiveProjection is not { } projection)
            {
                return;
            }

            if (m_SelectedSurface is not { } surface)
            {
                return;
            }

            Undo.RecordObject(target, "Modify Projection Surface");
            DrawSurfaceOutline(surface);
            DrawSurfaceHandles(surface);
            EditorUtility.SetDirty(target);
        }

        static void DrawSurfaceOutline(TrackedPerspectiveSurface surface)
        {
            var corners = surface.GetVertices();
            Handles.DrawLines(corners, new []{0, 1, 0, 2, 1, 3, 2, 3});
        }

        static void DrawSurfaceHandles(TrackedPerspectiveSurface surface)
        {
            surface.Rotation = Handles.RotationHandle(surface.Rotation, surface.Position);
            surface.Position = Handles.PositionHandle(surface.Position, surface.Rotation);
        }

        void OnRemoveSurfaceElement(ReorderableList list)
        {
            var obj = m_SurfacesProp.GetArrayElementAtIndex(list.index).managedReferenceValue as TrackedPerspectiveSurface;
            m_SurfacesProp.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
            System.Diagnostics.Debug.Assert(obj != null, nameof(obj) + " != null");

            if (obj == m_SelectedSurface)
            {
                m_SelectedSurface = null;
            }
        }
        
        void OnDrawSurfaceElement(Rect rect, int index, bool active, bool focused)
        {
            var element = m_SurfacesProp.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
            // EditorGUI.LabelField(rect, element.objectReferenceValue.name);
            // Selection.activeObject = element.objectReferenceValue;
        }

        void DisplayAddSurfaceDropdown(Rect buttonRect, ReorderableList list)
        {
            serializedObject.Update();
            var menu = new GenericMenu();
            // var existing = SceneUtils.FindAllObjectsInScene<TrackedPerspectiveSurface>();
            // foreach (var surface in existing)
            // {
            //     if (!IsSurfaceInList(surface))
            //     {
            //         menu.AddItem(new GUIContent(surface.name), false, () =>
            //         {
            //             AddSurfaceToList(surface);
            //         });
            //     }
            // }

            menu.AddItem(Labels.GetGUIContent(Labels.Field.DefaultProjectionSurface), false, AddDefaultSurface);
            menu.ShowAsContext();
        }

        void AddSurfaceToList(TrackedPerspectiveSurface surface)
        {
            var index = m_SurfacesProp.arraySize;
            m_SurfacesProp.InsertArrayElementAtIndex(index);
            m_SurfacesProp.GetArrayElementAtIndex(index).managedReferenceValue = surface;
            serializedObject.ApplyModifiedProperties();
            // EditorGUIUtility.PingObject(surface);
        }

        void AddDefaultSurface()
        {
            if (target is TrackedPerspectiveProjection parent)
            {
                var surface = TrackedPerspectiveSurface.CreateDefaultPlanar();
                // Undo.RegisterCreatedObjectUndo(surface.gameObject, k_UndoCreateSurface);
                AddSurfaceToList(surface);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_DebugProp, Labels.GetGUIContent(Labels.Field.Debug));
                m_SurfacesList.DoLayoutList();
                // EditorGUILayout.PropertyField(m_SurfacesProp);
                EditorGUILayout.PropertyField(m_NodeIndexProp, Labels.GetGUIContent(Labels.Field.NodeIndexOverride));

                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        void OnDisable()
        {
            PerspectiveSurfacePreview.DisableAll();
        }
    }

    [CustomPropertyDrawer(typeof(TrackedPerspectiveSurface))]
    class TrackedPerspectiveSurfaceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative("m_Name"), GUIContent.none);
        }
    }
}
