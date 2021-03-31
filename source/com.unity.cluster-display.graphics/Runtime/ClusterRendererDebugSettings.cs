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
        public interface IDebugSettingsReceiver
        {
            void OnChangeLayoutMode(ClusterRenderer.LayoutMode newLayoutMode);
            void ToggleClusterDisplayShaderKeywords(bool keywordsEnabled);
        }

        public delegate void OnChangeLayoutMode(ClusterRenderer.LayoutMode newLayoutMode);
        public delegate void OnEnableKeywords(bool keywordsEnabled);

        private OnChangeLayoutMode onChangeLayoutMode;
        private OnEnableKeywords onEnableKeywords;

        public void RegisterDebugSettingsReceiver (IDebugSettingsReceiver debugSettingsReceiver)
        {
            onChangeLayoutMode += debugSettingsReceiver.OnChangeLayoutMode;
            onEnableKeywords += debugSettingsReceiver.ToggleClusterDisplayShaderKeywords;

            debugSettingsReceiver.OnChangeLayoutMode(m_LayoutMode);
            debugSettingsReceiver.ToggleClusterDisplayShaderKeywords(m_EnableKeyword);
        }

        int m_TileIndexOverride;
        /// <summary>
        /// Tile index to be used in debug mode (overriding the one provided by ClusterDisplay.Sync).
        /// </summary>
        public int TileIndexOverride
        {
            get => m_TileIndexOverride;
            set { m_TileIndexOverride = value; }
        }

        ClusterRenderer.LayoutMode m_LayoutMode;
        public ClusterRenderer.LayoutMode CurrentLayoutMode
        {
            get => m_LayoutMode;
            set
            {
                if (value == m_LayoutMode)
                    return;

                m_LayoutMode = value;
                if (onChangeLayoutMode != null)
                    onChangeLayoutMode(m_LayoutMode);
            }
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
            set
            {
                if (value == m_EnableKeyword)
                    return;

                m_EnableKeyword = value;
                if (onEnableKeywords != null)
                    onEnableKeywords(m_EnableKeyword);
            }
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
            CurrentLayoutMode = ClusterRenderer.LayoutMode.XRTile;
            m_EnableKeyword = true;
            m_ViewportSubsection = new Rect(0, 0, 1, 1);
            m_UseDebugViewportSubsection = false;
            m_ScaleBiasTexOffset = Vector2.zero;
        }
    }
}
