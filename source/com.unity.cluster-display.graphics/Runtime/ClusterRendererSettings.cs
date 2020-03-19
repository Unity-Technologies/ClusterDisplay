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
        Vector2Int m_GridSize;
        
        /// <summary>
        /// Cluster Grid size expressed in tiles.
        /// </summary>
        public Vector2Int GridSize
        {
            get => m_GridSize;
            set => m_GridSize = value;
        }
        
        [SerializeField]
        Vector2 m_bezel;
        
        /// <summary>
        /// Bezel of the screen, expressed in mm.
        /// </summary>
        public Vector2 Bezel
        {
            get => m_bezel;
            set => m_bezel = value;
        }
        
        [SerializeField]
        Vector2 m_PhysicalScreenSize;
        
        /// <summary>
        /// Physical size of the screen in mm. Used to compute bezel.
        /// </summary>
        public Vector2 PhysicalScreenSize
        {
            get => m_PhysicalScreenSize;
            set => m_PhysicalScreenSize = value;
        }
        
        [SerializeField]
        int m_OverscanInPixels;
        /// <summary>
        /// Amount of overscan per tile expressed in pixels.
        /// </summary>
        public int OverscanInPixels
        {
            get { return m_OverscanInPixels; }
            set { m_OverscanInPixels = value; }
        }
    }
}
