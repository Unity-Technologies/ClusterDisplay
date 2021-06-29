using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Networking
{
    [CustomEditor(typeof(ComponentReflectorBase), editorForChildClasses: true)]
    public class ComponentReflectorBaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();
        }
    }
}
