using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.ClusterDisplay.Graphics.Editor;

namespace Unity.ClusterDisplay.Graphics
{
    static class ClusterDisplayGraphicsSetup
    {
        [MenuItem("Cluster Display/Setup Cluster Rendering")]
        static void SetupComponents()
        {
            GameObject gameObject;
            var instances = Object.FindObjectsOfType<ClusterRenderer>();
            if (instances.FirstOrDefault() is { } clusterRenderer)
            {
                gameObject = clusterRenderer.gameObject;
                Debug.LogWarning("A ClusterRenderer already exists in the scene.");
            }
            else
            {
                gameObject = new GameObject("ClusterDisplay");
                clusterRenderer = gameObject.AddComponent<ClusterRenderer>();
                gameObject.AddComponent<ClusterRendererCommandLineUtils>();
                ClusterRendererInspector.SetProjectionPolicy(clusterRenderer, typeof(TiledProjection));
                ClusterRendererInspector.AddMissingClusterCameraComponents();

                Undo.RegisterCreatedObjectUndo(gameObject, "Created Cluster Display object");
                Debug.Log("Added ClusterRenderer to the scene");
            }

            if (!gameObject.TryGetComponent<ClusterRendererCommandLineUtils>(out _))
            {
                Undo.AddComponent<ClusterRendererCommandLineUtils>(gameObject);
                Debug.Log("Added ClusterRendererCommandLineUtils");
            }

            EditorGUIUtility.PingObject(gameObject);

            Debug.Log("Cluster Rendering setup complete.");
        }
    }
}
