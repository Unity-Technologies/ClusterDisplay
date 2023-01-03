using UnityEditor;

namespace Unity.ClusterDisplay.Graphics.Editor
{
    static class ClusterRenderingSettingsProvider
    {
        static readonly string k_SettingsPath = "Project/ClusterDisplaySettings/ClusterRendering";

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider() =>
            new(k_SettingsPath, SettingsScope.Project)
            {
                label = "Cluster Rendering",
                activateHandler = (searchContext, parentElement) =>
                {

                }
            };
    }
}
