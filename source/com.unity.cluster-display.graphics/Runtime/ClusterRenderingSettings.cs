using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class ClusterRenderingSettings : ProjectSettings<ClusterRenderingSettings>
    {
        [SerializeField]
        bool m_EnableOnPlay;

        [SerializeField]
        bool m_PersistOnSceneChange;

        protected override void InitializeInstance()
        {
            m_EnableOnPlay = true;
            m_PersistOnSceneChange = true;
        }
    }
}
