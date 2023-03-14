using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    [RequireComponent(typeof(Text))]
    public class ClusterSyncBenchmarkUI : MonoBehaviour
    {
        Text m_DebugText;

        Text DebugText
        {
            get
            {
                if (m_DebugText == null)
                {
                    m_DebugText = this.GetComponent<Text>();
                    if (m_DebugText == null)
                        throw new System.Exception($"There is no: {nameof(Text)} component attached to: \"{gameObject.name}\".");
                }

                return m_DebugText;
            }
        }

        void Start()
        {
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync))
            {
                DebugText.text = clusterSync.IsClusterLogicEnabled
                    ? $"Node {clusterSync.NodeID}"
                    : "Cluster Rendering inactive";
            }
            else
            {
                DebugText.text = "Cluster Rendering not initialized";
            }
        }

        void Update()
        {
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) && clusterSync.IsClusterLogicEnabled)
            {
                DebugText.text = clusterSync.GetDiagnostics();
            }
        }
    }
}
