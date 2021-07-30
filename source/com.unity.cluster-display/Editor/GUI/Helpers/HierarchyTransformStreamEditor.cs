using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    [CustomEditor(typeof(HierarchyTransformStream))]
    public class HierarchyTransformStreamEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}
