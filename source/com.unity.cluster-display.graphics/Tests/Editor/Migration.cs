using Unity.ClusterDisplay.Graphics.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools.Graphics;
using ImageComparisonSettings = Unity.ClusterDisplay.Graphics.Tests.ImageComparisonSettings;

namespace Unity.ClusterDisplay.Graphics.EditorTests
{
    public class Migration
    {
        [MenuItem ("Cluster Display/Migrate Image Settings")]
        static void MigrateImageSettings()
        {
            foreach (EditorBuildSettingsScene buildSettingsScene in EditorBuildSettings.scenes)
            {
                var scene = EditorSceneManager.OpenScene(buildSettingsScene.path);

                var imageComparisonSettings = Object.FindObjectsOfType<ImageComparisonSettings>();
                if (imageComparisonSettings.Length == 0)
                {
                    Debug.LogError($"Could not find component {nameof(ImageComparisonSettings)} in scene \"{buildSettingsScene.path}\"");
                }
                if (imageComparisonSettings.Length > 1)
                {
                    Debug.LogError($"Could found multiple components {nameof(ImageComparisonSettings)} in scene \"{buildSettingsScene.path}\"");
                }

                imageComparisonSettings[0].CopyFromImageSettings();
                
                EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem ("Cluster Display/Check No Graphics Test Settings")]
        static void CheckNoGraphicsTestSettings()
        {
            foreach (EditorBuildSettingsScene buildSettingsScene in EditorBuildSettings.scenes)
            {
                EditorSceneManager.OpenScene(buildSettingsScene.path);

                var graphicsTestSettingsArray = Object.FindObjectsOfType<GraphicsTestSettings>();
                if (graphicsTestSettingsArray.Length != 0)
                {
                    Debug.LogError($"Could not found component(s) {nameof(GraphicsTestSettings)} in scene \"{buildSettingsScene.path}\"");
                }
               
            }
        }
    }
}
