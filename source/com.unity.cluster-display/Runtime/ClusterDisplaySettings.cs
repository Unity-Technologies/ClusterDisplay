using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class ClusterDisplaySettings : ProjectSettings<ClusterDisplaySettings>
    {
        [SerializeField]
        [Tooltip("Whether Cluster Display synchronization should be activated automatically on game start")]
        bool m_EnableOnPlay = true;

        [SerializeField]
        ClusterParams m_ClusterParams;

        public bool EnableOnPlay => m_EnableOnPlay;

        public ClusterParams ClusterParams => m_ClusterParams;

        protected override void InitializeInstance()
        {
            m_ClusterParams = ClusterParams.Default;
        }
    }
}
