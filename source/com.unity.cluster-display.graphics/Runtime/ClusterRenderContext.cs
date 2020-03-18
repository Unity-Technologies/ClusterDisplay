using Unity.ClusterRendering;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // Gives to custom layouts a centralized place to read properties from.
    // Note that some properties are directly forwarded from settings while others are inferred.
    class ClusterRenderContext
    {
        ClusterRendererSettings m_Settings;
        public ClusterRendererSettings Settings
        {
            set { m_Settings = value; }
        }

        ClusterRendererDebugSettings m_DebugSettings;
        public ClusterRendererDebugSettings DebugSettings
        {
            set { m_DebugSettings = value; }
        }
        
        bool m_Debug;
        public bool Debug
        {
            set { m_Debug = value; }
        }

        public int OverscanInPixels
        {
            get { return m_Settings.OverscanInPixels; }
        }

        public Vector2Int GridSize
        {
            get { return m_Settings.GridSize; }
        }
        
        public Vector2 Bezel
        {
            get { return m_Settings.Bezel; }
        }
        
        public Vector2 PhysicalScreenSize
        {
            get { return m_Settings.PhysicalScreenSize; }
        }

        public Vector2 DebugScaleBiasTexOffset
        {
            get { return m_Debug ? m_DebugSettings.ScaleBiasTexOffset : Vector2.zero; }
        }

        public int TileIndex
        {
            get
            {
                if (m_Debug || !ClusterSynch.Active)
                    return m_DebugSettings.TileIndexOverride;
                return ClusterSynch.Instance.DynamicLocalNodeId;
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
