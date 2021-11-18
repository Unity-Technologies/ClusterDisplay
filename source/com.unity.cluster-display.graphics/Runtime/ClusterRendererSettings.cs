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
        ClusterDisplayGraphicsResources m_Resources;

        public ClusterDisplayGraphicsResources resources
        {
            get => m_Resources;
            set => m_Resources = value;
        }

        [SerializeField]
        Vector2Int m_GridSize = new Vector2Int(2, 2);
        
        /// <summary>
        /// Cluster Grid size expressed in tiles.
        /// </summary>
        public Vector2Int gridSize
        {
            get => m_GridSize;
            set => m_GridSize = value;
        }
        
        [SerializeField]
        Vector2 m_bezel;
        
        /// <summary>
        /// Bezel of the screen, expressed in mm.
        /// </summary>
        public Vector2 bezel
        {
            get => m_bezel;
            set => m_bezel = value;
        }

        [SerializeField]
        Vector2 m_PhysicalScreenSize;
        
        /// <summary>
        /// Physical size of the screen in mm. Used to compute bezel.
        /// </summary>
        public Vector2 physicalScreenSize
        {
            get => m_PhysicalScreenSize;
            set => m_PhysicalScreenSize = value;
        }
        
        [SerializeField]
        int m_OverscanInPixels;
        /// <summary>
        /// Amount of overscan per tile expressed in pixels.
        /// </summary>
        public int overScanInPixels
        {
            get { return m_OverscanInPixels; }
            set { m_OverscanInPixels = value; }
        }

        [SerializeField]
        bool m_QueueEmitterFrames = true;
        public bool queueEmitterFrames
        {
            get => m_QueueEmitterFrames;
            set => m_QueueEmitterFrames = value;
        }

        [SerializeField]
        int m_QueueEmitterFrameCount = 1;
        public int queueEmitterFrameCount
        {
            get => m_QueueEmitterFrameCount;
            set => m_QueueEmitterFrameCount = value > 0 ? value : 1;
        }
    }
}
