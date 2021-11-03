using UnityEngine;
using UnityEditor;

namespace Unity.ClusterDisplay.Graphics
{
    // we update settings from the editor because we do not want to override build settings silently.
    [InitializeOnLoad]
    class ClusterBuildSettings
    {
        const int k_VSyncCount = 1;

        static ClusterBuildSettings()
        {
            EditorApplication.update += Update;
        }

        public static void Update()
        {
            if (QualitySettings.vSyncCount != k_VSyncCount)
            {
                QualitySettings.vSyncCount = k_VSyncCount;
                Debug.Log("<b>[ClusterDisplay]</b> automatically updated settings: <b>VSync Count</b> has been set to <b>Every V Blank</b>.");
            }

            EditorApplication.update -= Update;
        }
    }
}
