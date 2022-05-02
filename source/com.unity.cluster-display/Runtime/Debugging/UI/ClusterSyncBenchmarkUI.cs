using Unity.ClusterDisplay;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Text))]
    public class ClusterSyncBenchmarkUI : MonoBehaviour
    {
        private int m_Frames;
        private Text m_DebugText;

        private Text debugText
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
            if (ServiceLocator.TryGet(out ClusterSync clusterSync))
            {
                debugText.text = clusterSync.StateAccessor.IsActive
                    ? $"Node {clusterSync.StateAccessor.NodeID}"
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
            if (ServiceLocator.TryGet(out ClusterSync clusterSync) && clusterSync.StateAccessor.IsActive)
            {
                debugText.text = clusterSync.GetDebugString();
            }
        }
    }
}
