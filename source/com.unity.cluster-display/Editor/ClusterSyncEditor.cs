using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Editor
{
    [CustomEditor(typeof(ClusterSync))]
    public class ClusterSyncEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var clusterSync = target as ClusterSync;

            if (GUILayout.Button("Play as Emitter"))
            {
                clusterSync.SetupForEditorTesting(isEmitter: true);
                EditorUtility.SetDirty(clusterSync);
                EditorApplication.EnterPlaymode();
            }

            if (GUILayout.Button("Play as Repeater"))
            {
                clusterSync.SetupForEditorTesting(isEmitter: false);
                EditorUtility.SetDirty(clusterSync);
                EditorApplication.EnterPlaymode();
            }
        }
    }
}
