using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    class ClusterDisplaySettings : ProjectSettings<ClusterDisplaySettings>
    {
        [SerializeField]
        bool m_EnableOnPlay = true;

        [SerializeField]
        ClusterParams m_ClusterParams;

        [SerializeField]
        MissionControlSettings m_MissionControlSettings = new();

        public bool EnableOnPlay => m_EnableOnPlay;

        public ClusterParams ClusterParams => m_ClusterParams;

        public MissionControlSettings MissionControlSettings => m_MissionControlSettings;

        protected override void InitializeInstance()
        {
            m_ClusterParams = ClusterParams.Default;
        }
    }
}
