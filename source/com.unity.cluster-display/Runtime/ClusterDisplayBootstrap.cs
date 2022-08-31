using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay
{
    [DefaultExecutionOrder(int.MinValue)]
    [ExecuteAlways]
    public class ClusterDisplayBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (!ClusterDisplayManager.TryGetInstance(out var instance, logError: false))
            {
                ClusterDebug.Log($"There is no instance of: \"{nameof(ClusterDisplayManager)}\" in the scene, creating...");
                if (!TryGetComponent<ClusterDisplayManager>(out var clusterDisplayManager))
                {
                    clusterDisplayManager = gameObject.AddComponent<ClusterDisplayManager>();
                }
                clusterDisplayManager.hideFlags = HideFlags.DontSave;
            }

            if (Application.isPlaying)
                Destroy(this);
        }
    }
}
