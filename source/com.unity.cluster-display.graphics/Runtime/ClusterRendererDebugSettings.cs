using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Debug settings for the Cluster Renderer.
    /// Those are both meant to debug the ClusterRenderer itself,
    /// and external graphics related ClusterDisplay code.
    /// </summary>
    [System.Serializable]
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

        public void UnRegisterDebugSettingsReceiver (IDebugSettingsReceiver debugSettingsReceiver)
        {
            onChangeLayoutMode -= debugSettingsReceiver.OnChangeLayoutMode;
            onEnableKeywords -= debugSettingsReceiver.ToggleClusterDisplayShaderKeywords;
        }

        [SerializeField] int m_TileIndexOverride;
        /// <summary>
        /// Tile index to be used in debug mode (overriding the one provided by ClusterDisplay.Sync).
        /// </summary>
        public int tileIndexOverride
        {
            get => m_TileIndexOverride;
            set { m_TileIndexOverride = value; }
        }

        [SerializeField] ClusterRenderer.LayoutMode m_LayoutMode;
        public ClusterRenderer.LayoutMode currentLayoutMode
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
        [SerializeField] bool m_EnableKeyword;
        public bool enableKeyword
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
        [SerializeField] Rect m_ViewportSubsection;
        public Rect viewportSubsection
        {
            get => m_ViewportSubsection;
            set => m_ViewportSubsection = value;
        }

        /// <summary>
        /// Allows the viewport subsection to be directly controlled from the inspector,
        /// instead of being inferred from tile index and grid size.
        /// </summary>
        [SerializeField] bool m_UseDebugViewportSubsection;
        public bool useDebugViewportSubsection
        {
            set { m_UseDebugViewportSubsection = value; }
            get { return m_UseDebugViewportSubsection; }
        }

        [SerializeField] Vector2 m_ScaleBiasTexOffset;
        /// <summary>
        /// Allows visualization of overscanned pixels in the final render.
        /// </summary>
        public Vector2 scaleBiasTextOffset
        {
            get => m_ScaleBiasTexOffset;
            set => m_ScaleBiasTexOffset = value;
        }

        [SerializeField] Color m_BezelColor;
        public Color bezelColor
        {
            get => m_BezelColor;
            set => m_BezelColor = value;
        }
        
        public ClusterRendererDebugSettings() { Reset(); }
        
        public void Reset()
        {
            m_TileIndexOverride = 0;
            currentLayoutMode = ClusterRenderer.LayoutMode.StandardTile;
            m_EnableKeyword = true;
            m_ViewportSubsection = new Rect(0, 0, 1, 1);
            m_UseDebugViewportSubsection = false;
            m_ScaleBiasTexOffset = Vector2.zero;
        }
    }
}
