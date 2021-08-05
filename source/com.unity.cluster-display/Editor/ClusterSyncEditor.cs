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

            if (GUILayout.Button("Play as Master"))
            {
                clusterSync.SetupForEditorTesting(isMaster: true);
                EditorUtility.SetDirty(clusterSync);
                EditorApplication.EnterPlaymode();
            }

            if (GUILayout.Button("Play as Slave"))
            {
                clusterSync.SetupForEditorTesting(isMaster: false);
                EditorUtility.SetDirty(clusterSync);
                EditorApplication.EnterPlaymode();
            }
        }
    }
}
