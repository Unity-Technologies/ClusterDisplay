using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay
{
    [CustomEditor(typeof(FilteredTransformHierarchyStream))]
    public class FilteredTransformHierarchyStreamEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}
