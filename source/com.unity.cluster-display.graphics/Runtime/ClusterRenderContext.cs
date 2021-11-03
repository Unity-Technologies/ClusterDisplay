using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // Gives to custom layouts a centralized place to read properties from.
    // Note that some properties are directly forwarded from settings while others are inferred.
    [System.Serializable]
    public class ClusterRenderContext
    {
        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();
        public ClusterRendererSettings settings => m_Settings;

        [SerializeField]
        ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();
        public ClusterRendererDebugSettings debugSettings => m_DebugSettings;

        bool m_Debug;

        public bool debug
        {
            get => m_Debug;
            set { m_Debug = value; }
        }

        public int overscanInPixels => m_Settings.overScanInPixels;
        public Vector2Int gridSize => m_Settings.gridSize;
        public Vector2 bezel => m_Settings.bezel;
        public Vector2 physicalScreenSize => m_Settings.physicalScreenSize;
        public Vector2 debugScaleBiasTexOffset => m_Debug ? m_DebugSettings.scaleBiasTextOffset : Vector2.zero;
        public Color bezelColor => m_DebugSettings.bezelColor;

        public int tileIndex
        {
            get
            {
                if (m_Debug || !ClusterSync.Active)
                    return m_DebugSettings.tileIndexOverride;
                return ClusterSync.Instance.DynamicLocalNodeId;
            }
        }

        // Can pass index otherwise the current tile index will be used.
        public Rect GetViewportSubsection(int tileIndex = -1)
        {
            if (m_Debug && m_DebugSettings.useDebugViewportSubsection)
                return m_DebugSettings.viewportSubsection;

            return GraphicsUtil.TileIndexToViewportSection(gridSize, tileIndex == -1 ? this.tileIndex : tileIndex);
        }

        // We assume all cluster screens have the same resolution, otherwise we couldn't just infer global screen size.
        public Vector2 globalScreenSize
        {
            get { return new Vector2(gridSize.x * Screen.width, gridSize.x * Screen.width); }
        }
    }
}
