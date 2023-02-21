using Unity.ClusterDisplay.Utils;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    class ClusterRenderingSettings : ProjectSettings<ClusterRenderingSettings>
    {
        [SerializeField]
        [Tooltip("Whether the active Cluster Renderer should persist after a scene change")]
        bool m_PersistOnSceneChange;

        public bool PersistOnSceneChange => m_PersistOnSceneChange;

        protected override void InitializeInstance()
        {
            m_PersistOnSceneChange = true;
        }
    }
}
