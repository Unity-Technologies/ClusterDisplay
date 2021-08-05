using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    [CustomEditor(typeof(SimpleTransformHierarchyStream))]
    public class SimpleTransformHierarchyStreamEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}
