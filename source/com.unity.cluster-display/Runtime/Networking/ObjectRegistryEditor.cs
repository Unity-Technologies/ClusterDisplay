using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ObjectRegistry : ScriptableObject
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ObjectRegistry))]
    public class ObjectRegistryEditor : Editor
    {
        public Object targetObject;
        public System.Type targetType;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Reset"))
                (target as ObjectRegistry).Reset();
        }
    }
#endif
}
