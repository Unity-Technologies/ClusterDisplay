using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// User exposed setting for the Cluster Renderer.
    /// </summary>
    [System.Serializable]
    public sealed class ClusterRendererSettings
    {
        // TODO Could be a property on ClusterRenderer unless new settings are added.

        [SerializeField]
        [Range(0, 256)]
        int m_OverscanInPixels;

        [SerializeField]
        bool m_RenderTestPattern;

        /// <summary>
        /// Amount of overscan per tile expressed in pixels.
        /// </summary>
        public int OverScanInPixels
        {
            get => m_OverscanInPixels;
            set => m_OverscanInPixels = value;
        }

        public bool RenderTestPattern
        {
            get => m_RenderTestPattern;
            set => m_RenderTestPattern = value;
        }
    }
}
