using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [CustomEditor(typeof(CameraContextRegistry))]
    public class CameraContextRegistryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var cameraContextRegistry = target as CameraContextRegistry;
            if (cameraContextRegistry == null)
            {
                return;
            }

            if (GUILayout.Button("Flush Registry"))
            {
                cameraContextRegistry.Flush();
            }

            var cameraContextTargets = cameraContextRegistry.cameraContextTargets;
            for (var i = 0; i < cameraContextTargets.Length; i++)
            {
                EditorGUILayout.LabelField(cameraContextTargets[i].gameObject.name);
            }
        }
    }
}
