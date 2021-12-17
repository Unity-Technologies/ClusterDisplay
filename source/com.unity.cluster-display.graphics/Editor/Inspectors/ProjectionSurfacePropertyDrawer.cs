using System;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    [CustomPropertyDrawer(typeof(ProjectionSurface))]
    class ProjectionSurfacePropertyDrawer : PropertyDrawer
    {
        public static float GetHeight(SerializedProperty property)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
            var height = lineHeight;
            if (property.isExpanded)
            {
                height += lineHeight * 4;
            }

            return height;
        }
        
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var nameProp = property.FindPropertyRelative("Name");
            var resolution = property.FindPropertyRelative("ScreenResolution");
            var sizeProp = property.FindPropertyRelative("PhysicalSize");
            var positionProp = property.FindPropertyRelative("LocalPosition");
            var rotationProp = property.FindPropertyRelative("LocalRotation");

            var position = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.indentLevel++;
            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, 10, lineHeight),
                property.isExpanded,
                property.isExpanded ? string.Empty : nameProp.stringValue);

            if (property.isExpanded)
            {
                EditorGUI.PropertyField(position, nameProp);
                position.y += lineHeight;
                EditorGUI.PropertyField(position, resolution);
                position.y += lineHeight;
                EditorGUI.PropertyField(position, sizeProp);
                position.y += lineHeight;
                EditorGUI.PropertyField(position, positionProp);
                position.y += lineHeight;
                EditorGUI.PropertyField(position, rotationProp);
            }

            EditorGUI.indentLevel--;
        }
    }
}
