using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.ClusterDisplay;
using UnityEngine.UIElements;
using System;

namespace Unity.ClusterDisplay.Editor.Inspectors
{
    public abstract class PropertyDrawerExtension<T> : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width - 25, position.height), property);
            if (GUI.Button(new Rect(position.width - 5, position.y, 25, position.height), "->"))
            {
                var targetObject = property.serializedObject.targetObject;
                var targetObjectType = targetObject.GetType();

                if (ReflectionUtils.IsAssemblyPostProcessable(targetObjectType.Assembly))
                {
                }
            }
        }
    }
}
