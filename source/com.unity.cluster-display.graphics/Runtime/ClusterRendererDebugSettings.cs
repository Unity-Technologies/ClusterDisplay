using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Debug settings for the Cluster Renderer.
    /// Those are both meant to debug the ClusterRenderer itself,
    /// and external graphics related ClusterDisplay code.
    /// </summary>
    public class ClusterRendererDebugSettings
    {
        int m_TileIndexOverride;
        /// <summary>
        /// Tile index to be used in debug mode (overriding the one provided by ClusterDisplay.Sync).
        /// </summary>
        public int TileIndexOverride
        {
            get => m_TileIndexOverride;
            set { m_TileIndexOverride = value; }
        }

        bool m_EnableStitcher;
        /// <summary>
        /// Enables the Stitcher, a custom layout providing a local preview of the total cluster output.
        /// </summary>
        /// <remarks>
        /// As the Stitcher will render all tiles and compose them, a significant performance cost can be expected.
        /// </remarks>
        public bool EnableStitcher
        {
            get => m_EnableStitcher;
            set { m_EnableStitcher = value; }
        }
        
        /// <summary>
        /// Enable/Disable ClusterDisplay shader features, such as Global Screen Space,
        /// meant to compare original and ported-to-cluster-display shaders,
        /// in order to observe cluster-display specific artefacts.
        /// </summary>
        bool m_EnableKeyword;
        public bool EnableKeyword
        {
            get => m_EnableKeyword;
            set { m_EnableKeyword = value; }
        }
        
        /// <summary>
        /// Allows direct control of the viewport subsection.
        /// </summary>
        Rect m_ViewportSubsection;
        public Rect ViewportSubsection
        {
            get => m_ViewportSubsection;
            set => m_ViewportSubsection = value;
        }

        /// <summary>
        /// Allows the viewport subsection to be directly controlled from the inspector,
        /// instead of being inferred from tile index and grid size.
        /// </summary>
        bool m_UseDebugViewportSubsection;
        public bool UseDebugViewportSubsection
        {
            set { m_UseDebugViewportSubsection = value; }
            get { return m_UseDebugViewportSubsection; }
        }

        Vector2 m_ScaleBiasTexOffset;
        /// <summary>
        /// Allows visualization of overscanned pixels in the final render.
        /// </summary>
        public Vector2 ScaleBiasTexOffset
        {
            get => m_ScaleBiasTexOffset;
            set => m_ScaleBiasTexOffset = value;
        }
        
        public ClusterRendererDebugSettings() { Reset(); }
        
        public void Reset()
        {
            m_TileIndexOverride = 0;
            m_EnableStitcher = false;
            m_EnableKeyword = true;
            m_ViewportSubsection = new Rect(0, 0, 1, 1);
            m_UseDebugViewportSubsection = false;
            m_ScaleBiasTexOffset = Vector2.zero;
        }
    }
}
