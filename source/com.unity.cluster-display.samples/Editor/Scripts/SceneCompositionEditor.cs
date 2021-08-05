using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(SceneComposition))]
    public class SceneCompositionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var sceneComposition = target as SceneComposition;

            if (GUILayout.Button("Stage"))
                sceneComposition.Stage();

            if (GUILayout.Button("Open"))
                sceneComposition.Open();
        }
    }
}
