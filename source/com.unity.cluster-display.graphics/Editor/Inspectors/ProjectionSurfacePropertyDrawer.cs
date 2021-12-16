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
            var foldout = property.FindPropertyRelative("m_Expanded");
            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
            var height = lineHeight;
            if (foldout.boolValue)
            {
                height += lineHeight * 4;
            }

            return height;
        }
        
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var nameProp = property.FindPropertyRelative("Name");
            var foldout = property.FindPropertyRelative("m_Expanded");
            var resolution = property.FindPropertyRelative("ScreenResolution");
            var sizeProp = property.FindPropertyRelative("PhysicalSize");
            var positionProp = property.FindPropertyRelative("LocalPosition");
            var rotationProp = property.FindPropertyRelative("LocalRotation");

            var position = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            var lineHeight = EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.indentLevel++;
            foldout.boolValue = EditorGUI.Foldout(new Rect(position.x, position.y, 10, lineHeight),
                foldout.boolValue,
                foldout.boolValue ? string.Empty : nameProp.stringValue);

            if (foldout.boolValue)
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
