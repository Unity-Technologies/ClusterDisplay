using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// User exposed setting for the Cluster Renderer.
    /// </summary>
    [System.Serializable]
    public sealed class ClusterRendererSettings
    {
        [SerializeField]
        [Range(0, 256)]
        int m_OverscanInPixels;
        
        /// <summary>
        /// Amount of overscan per tile expressed in pixels.
        /// </summary>
        public int OverScanInPixels
        {
            get => m_OverscanInPixels;
            set => m_OverscanInPixels = value;
        }
    }
}
