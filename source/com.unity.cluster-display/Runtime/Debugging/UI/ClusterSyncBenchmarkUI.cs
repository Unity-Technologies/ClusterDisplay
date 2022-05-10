using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Text))]
    public class ClusterSyncBenchmarkUI : MonoBehaviour
    {
        int m_Frames;
        Text m_DebugText;

        Text debugText
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
            m_Frames = 0;
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync))
            {
                debugText.text = clusterSync.IsClusterLogicEnabled
                    ? $"Node {clusterSync.NodeID}"
                    : "Cluster Rendering inactive";
            }
            else
            {
                debugText.text = "Cluster Rendering not initialized";
            }
        }

        void Update()
        {
            m_Frames++;
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) && clusterSync.IsClusterLogicEnabled)
            {
                debugText.text = clusterSync.GetDiagnostics();
            }
        }
    }
}
