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
        [SerializeField]
        int m_TileIndexOverride;

        /// <summary>
        /// Tile index to be used in debug mode (overriding the one provided by ClusterDisplay.Sync).
        /// </summary>
        public int TileIndexOverride
        {
            get => m_TileIndexOverride;
            set => m_TileIndexOverride = value;
        }

        [SerializeField]
        LayoutMode m_LayoutMode;

        public LayoutMode LayoutMode
        {
            get => m_LayoutMode;
            set => m_LayoutMode = value;
        }

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

        /// <summary>
        /// Allows direct control of the viewport subsection.
        /// </summary>
        [SerializeField]
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
        [SerializeField]
        bool m_UseDebugViewportSubsection;

        public bool UseDebugViewportSubsection
        {
            set => m_UseDebugViewportSubsection = value;
            get => m_UseDebugViewportSubsection;
        }

        [SerializeField]
        Vector2 m_ScaleBiasTexOffset;

        /// <summary>
        /// Allows visualization of overscanned pixels in the final render.
        /// </summary>
        public Vector2 ScaleBiasTextOffset
        {
            get => m_ScaleBiasTexOffset;
            set => m_ScaleBiasTexOffset = value;
        }

        [SerializeField]
        Color m_BezelColor;

        public Color BezelColor
        {
            get => m_BezelColor;
            set => m_BezelColor = value;
        }

        public ClusterRendererDebugSettings()
        {
            Reset();
        }

        // TODO Use static factory?
        public void Reset()
        {
            m_TileIndexOverride = 0;
            LayoutMode = LayoutMode.StandardTile;
            m_EnableKeyword = true;
            m_ViewportSubsection = new Rect(0, 0, 1, 1);
            m_UseDebugViewportSubsection = false;
            m_ScaleBiasTexOffset = Vector2.zero;
        }
    }
}
