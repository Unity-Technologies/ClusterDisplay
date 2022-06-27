using System;
using System.Linq;
using System.Reflection;
using Unity.ClusterDisplay.Scripting;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(ClusterReplication))]
    public class ClusterReplicationEditor : UnityEditor.Editor
    {
        static class Contents
        {
            public static readonly GUIContent EditorLinkLabel = EditorGUIUtility.TrTextContent("Editor Link");
            public static readonly GUIContent TargetListHeader =
                EditorGUIUtility.TrTextContent("Replicated component properties");
            public static readonly GUIContent CreateLinkAssetButton =
                EditorGUIUtility.TrTextContent("Create new Link Config");
            public static readonly GUIContent InvalidPropertyIcon =
                EditorGUIUtility.TrIconContent("console.warnicon.sml", "This property cannot be replicated");
        }

        ClusterReplication m_ClusterReplication;
        ReorderableList m_TargetsList;
        SerializedProperty m_ReplicationTargets;
        SerializedProperty m_LinkConfigProperty;

        void OnEnable()
        {
            m_ClusterReplication = target as ClusterReplication;
            m_ReplicationTargets = serializedObject.FindProperty("m_ReplicationTargets");
            m_LinkConfigProperty = serializedObject.FindProperty("m_EditorLinkConfig");
            CreateTargetsList();
        }

        void CreateTargetsList()
        {
            if (m_TargetsList != null)
            {
                return;
            }

            m_TargetsList = new ReorderableList(serializedObject,
                m_ReplicationTargets,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, Contents.TargetListHeader),
                drawElementCallback = DrawElement,
                onAddDropdownCallback = (_, _) => ShowAddTargetMenu(),
                onRemoveCallback = RemoveTarget,
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 2
            };
        }

        void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            var element = m_ReplicationTargets.GetArrayElementAtIndex(index);
            var componentProp = element.FindPropertyRelative("m_Component");
            var componentTarget = componentProp.objectReferenceValue as Component;

            rect.y += 2;
            var leftRect = rect;
            leftRect.width = rect.width / 2 - 5;

            var rightRect = rect;
            rightRect.x = leftRect.xMax + 5;
            rightRect.xMax = rect.xMax - 21;

            var iconRect = rect;
            iconRect.width = 16;
            iconRect.x = rightRect.xMax + 5;
            iconRect.y -= 2;
            iconRect.height += 2;
            if (componentTarget)
            {
                using var changeScope = new EditorGUI.ChangeCheckScope();
                var allComponents = GetComponents();
                var selected = EditorGUI.Popup(leftRect, Array.IndexOf(allComponents, componentTarget), allComponents.Select(comp => comp.GetType().Name).ToArray());

                var selectedComponent = allComponents[selected];
                componentProp.objectReferenceValue = selectedComponent;

                if (!ClusterReplication.HasSpecializedReplicator(selectedComponent.GetType()))
                {
                    var propertyNames = GetPropertyNames(selectedComponent);
                    var propertyProp = element.FindPropertyRelative("m_Property");

                    var selectedPropertyIndex = EditorGUI.Popup(rightRect, Array.IndexOf(propertyNames, propertyProp.stringValue), propertyNames);

                    if (selectedPropertyIndex >= 0)
                    {
                        propertyProp.stringValue = propertyNames[selectedPropertyIndex];
                    }
                }

                if (changeScope.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }

                if (!element.FindPropertyRelative("m_IsValid").boolValue)
                    EditorGUI.LabelField(iconRect, Contents.InvalidPropertyIcon);
            }
        }

        Component[] GetComponents() => m_ClusterReplication.gameObject.GetComponents(typeof(Component))
            .Where(component => component != m_ClusterReplication).ToArray();

        void ShowAddTargetMenu()
        {
            var menu = new GenericMenu();
            foreach (var component in GetComponents())
            {
                menu.AddItem(new GUIContent(component.GetType().Name), on: false, () =>
                {
                    AddTarget(component);
                });
            }

            menu.ShowAsContext();
        }

        void AddTarget(Component component)
        {
            Undo.RegisterCompleteObjectUndo(m_ClusterReplication, "Add Replication Target");
            m_ClusterReplication.AddTarget(component);
            EditorUtility.SetDirty(m_ClusterReplication);
        }

        void RemoveTarget(ReorderableList list)
        {
            var removeIndex = list.index;
            m_ReplicationTargets.DeleteArrayElementAtIndex(removeIndex);
            serializedObject.ApplyModifiedProperties();
        }

        static string[] GetPropertyNames(object obj)
        {
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(info => info.GetSetMethod(nonPublic: false) != null && info.PropertyType.IsUnManaged())
                .Select(info => info.Name);
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(info => info.Name);
            return properties.Concat(fields).ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var linkChanged = new EditorGUI.ChangeCheckScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(m_LinkConfigProperty, Contents.EditorLinkLabel);
                    if (GUILayout.Button(Contents.CreateLinkAssetButton))
                    {
                        m_LinkConfigProperty.objectReferenceValue = EditorLinkConfigEditor.CreateLinkConfig();
                    }
                }

                if (linkChanged.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    m_ClusterReplication.OnEditorLinkChanged();
                }
            }

            m_TargetsList.DoLayoutList();
        }
    }
}
