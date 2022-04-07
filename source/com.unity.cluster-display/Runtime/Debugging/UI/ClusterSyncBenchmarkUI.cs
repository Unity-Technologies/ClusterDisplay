using Unity.ClusterDisplay;
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

            if (ClusterDisplayState.IsActive)
            {
                debugText.text = ClusterDisplayState.IsActive ? $"Node {ClusterDisplayState.NodeID}" : "Cluster Rendering inactive";
            }
        }

        void Update()
        {
            m_Frames++;

            if (ClusterDisplayState.IsActive)
            {
                debugText.text = ClusterSyncDebug.GetDebugString();
            }
        }
    }
}
