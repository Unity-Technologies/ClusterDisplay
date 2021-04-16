using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // Gives to custom layouts a centralized place to read properties from.
    // Note that some properties are directly forwarded from settings while others are inferred.
    [System.Serializable]
    public class ClusterRenderContext
    {
        [SerializeField] ClusterRendererSettings m_Settings = new ClusterRendererSettings();
        public ClusterRendererSettings Settings => m_Settings;

        [SerializeField] ClusterRendererDebugSettings m_DebugSettings = new ClusterRendererDebugSettings();
        public ClusterRendererDebugSettings DebugSettings => m_DebugSettings;
        
        [SerializeField] bool m_Debug;
        public bool Debug
        {
            get => m_Debug;
            set { m_Debug = value; }
        }

        public int OverscanInPixels => m_Settings.OverscanInPixels;
        public Vector2Int GridSize => m_Settings.GridSize;
        public Vector2 Bezel => m_Settings.Bezel;
        public Vector2 PhysicalScreenSize => m_Settings.PhysicalScreenSize;
        public Vector2 DebugScaleBiasTexOffset => m_Debug ? m_DebugSettings.ScaleBiasTexOffset : Vector2.zero;
        public Color BezelColor => m_DebugSettings.BezelColor;

        public int TileIndex
        {
            get
            {
                if (m_Debug || !ClusterSync.Active)
                    return m_DebugSettings.TileIndexOverride;
                return ClusterSync.Instance.DynamicLocalNodeId;
            }
        }
        
        // Can pass index otherwise the current tile index will be used.
        public Rect GetViewportSubsection(int tileIndex = -1)
        {
            if (m_Debug && m_DebugSettings.UseDebugViewportSubsection)
                return m_DebugSettings.ViewportSubsection;
            
            return GraphicsUtil.TileIndexToViewportSection(GridSize, tileIndex == -1 ? TileIndex : tileIndex);
        }

        // We assume all cluster screens have the same resolution, otherwise we couldn't just infer global screen size.
        public Vector2 GlobalScreenSize
        {
            get { return new Vector2(GridSize.x * Screen.width, GridSize.x * Screen.width); }
        }
    }
}
