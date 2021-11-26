using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Debug settings for the Cluster Renderer.
    /// Those are both meant to debug the ClusterRenderer itself,
    /// and external graphics related ClusterDisplay code.
    /// </summary>
    [Serializable]
    public class ClusterRendererDebugSettings
    {
        /// <summary>
        /// Enable/Disable ClusterDisplay shader features, such as Global Screen Space,
        /// meant to compare original and ported-to-cluster-display shaders,
        /// in order to observe cluster-display specific artefacts.
        /// </summary>
        [SerializeField]
        bool m_EnableKeyword;

        public bool EnableKeyword
        {
            get => m_EnableKeyword;
            set => m_EnableKeyword = value;
        }

        public ClusterRendererDebugSettings()
        {
            Reset();
        }

        // TODO Use static factory?
        public void Reset()
        {
            m_EnableKeyword = true;
        }
    }
}
