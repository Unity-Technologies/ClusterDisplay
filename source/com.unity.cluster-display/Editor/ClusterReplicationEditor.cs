using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.ClusterDisplay.Scripting;
using Unity.ClusterDisplay.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(ClusterReplication))]
    public class ClusterReplicationEditor : UnityEditor.Editor
    {
        ClusterReplication m_ClusterReplication;
        ReorderableList m_TargetsList;
        SerializedProperty m_ReplicationTargets;

        void OnEnable()
        {
            m_ClusterReplication = target as ClusterReplication;
            m_ReplicationTargets = serializedObject.FindProperty("m_ReplicationTargets");
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
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Replication Targets"),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = m_ReplicationTargets.GetArrayElementAtIndex(index);
                    var componentProp = element.FindPropertyRelative("m_Component");
                    var componentTarget = componentProp.objectReferenceValue as Component;
                    var leftRect = rect;
                    leftRect.width = rect.width / 2 - 5;
                    var rightRect = rect;
                    rightRect.x = leftRect.xMax + 5;
                    rightRect.xMax = rect.xMax;
                    if (componentTarget)
                    {
                        var allComponents = GetComponents();
                        var selected = EditorGUI.Popup(leftRect, Array.IndexOf(allComponents, componentTarget),
                            allComponents.Select(comp => comp.GetType().Name).ToArray());

                        var selectedComponent = allComponents[selected];
                        componentProp.objectReferenceValue = selectedComponent;

                        if (!ClusterReplication.HasSpecializedReplicator(selectedComponent.GetType()))
                        {
                            var propertyNames = GetPropertyNames(selectedComponent);
                            var propertyProp = element.FindPropertyRelative("m_Property");

                            var selectedPropertyIndex = EditorGUI.Popup(rightRect,
                                Array.IndexOf(propertyNames, propertyProp.stringValue), propertyNames);

                            if (selectedPropertyIndex >= 0)
                            {
                                propertyProp.stringValue = propertyNames[selectedPropertyIndex];
                            }
                        }
                    }
                },
                onAddDropdownCallback = (rect, list) => ShowAddTargetMenu()

            };
        }

        Component[] GetComponents() => m_ClusterReplication.gameObject.GetComponents(typeof(Component));

        void ShowAddTargetMenu()
        {
            var menu = new GenericMenu();
            var allComponents = GetComponents();
            foreach (var component in allComponents)
            {
                if (component == m_ClusterReplication) continue;
                menu.AddItem(new GUIContent(component.GetType().Name), on: false, () =>
                {
                    AddTarget(component);
                });
            }

            menu.ShowAsContext();
        }

        void AddTarget(Component component)
        {
            Undo.RegisterCompleteObjectUndo(m_ClusterReplication, "Add replication target");
            m_ClusterReplication.AddTarget(component, null);
            EditorUtility.SetDirty(m_ClusterReplication);
        }

        static string[] GetPropertyNames(object obj) =>
            obj.GetType().GetProperties()
                .Where(info => info.GetSetMethod(nonPublic: false) != null && info.PropertyType.IsUnManaged())
                .Select(info => info.Name).ToArray();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            m_TargetsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
