using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    struct Viewport
    {
        Vector2Int m_GridSize;
        Vector2 m_PhysicalScreenSize;
        Vector2 m_Bezel;
        int m_OverscanInPixels;

        public Viewport(Vector2Int gridSize, Vector2 physicalScreenSize, Vector2 bezel, int overscanInPixels)
        {
            m_GridSize = gridSize;
            m_PhysicalScreenSize = physicalScreenSize;
            m_Bezel = bezel;
            m_OverscanInPixels = overscanInPixels;
        }
        
        public Viewport(Vector2Int gridSize, int overscanInPixels)
        {
            m_GridSize = gridSize;
            m_PhysicalScreenSize = Vector2.zero;
            m_Bezel = Vector2.zero;
            m_OverscanInPixels = overscanInPixels;
        }
        
        public Viewport(Vector2Int gridSize)
        {
            m_GridSize = gridSize;
            m_PhysicalScreenSize = Vector2.zero;
            m_Bezel = Vector2.zero;
            m_OverscanInPixels = 0;
        }

        public Rect GetSubsectionWithoutOverscan(int tileIndex)
        {
            var viewportSubsection = TileIndexToSubSection(m_GridSize, tileIndex);

            if (!(Approximately(m_PhysicalScreenSize, Vector2.zero) || Approximately(m_Bezel, Vector2.zero)))
            {
                viewportSubsection = ApplyBezel(viewportSubsection, m_PhysicalScreenSize, m_Bezel);
            }
            
            return viewportSubsection;
        }
        
        public Rect GetSubsectionWithOverscan(int tileIndex)
        {
            var viewportSubsection = GetSubsectionWithoutOverscan(tileIndex);
            return ApplyOverscan(viewportSubsection, m_OverscanInPixels);
        }

        // there's no *right* way to do it, it simply is a convention
        public static Rect TileIndexToSubSection(Vector2Int gridSize, int tileIndex)
        {
            if (gridSize.x * gridSize.y == 0)
                return Rect.zero;
            var x = tileIndex % gridSize.x;
            var y = gridSize.y - 1 - tileIndex / gridSize.x; // tile 0 is top-left
            var dx = 1f / (float)gridSize.x;
            var dy = 1f / (float)gridSize.y;
            return new Rect(x * dx, y * dy, dx, dy);
        }

        static Rect Expand(Rect r, Vector2 delta)
        {
            return Rect.MinMaxRect(
                r.min.x - delta.x,
                r.min.y - delta.y,
                r.max.x + delta.x,
                r.max.y + delta.y);
        }

        static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels)
        {
            return ApplyOverscan(normalizedViewportSubsection, overscanInPixels, Screen.width, Screen.height);
        }

        static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels, int viewportWidth, int viewportHeight)
        {
            var normalizedOverscan = new Vector2(
                overscanInPixels * (normalizedViewportSubsection.max.x - normalizedViewportSubsection.min.x) / viewportWidth,
                overscanInPixels * (normalizedViewportSubsection.max.y - normalizedViewportSubsection.min.y) / viewportHeight);

            return Expand(normalizedViewportSubsection, normalizedOverscan);
        }

        static Rect ApplyBezel(Rect normalizedViewportSubsection, Vector2 physicalScreenSizeInMm, Vector2 bezelInMm)
        {
            var normalizedBezel = new Vector2(
                bezelInMm.x / (float)physicalScreenSizeInMm.x,
                bezelInMm.y / (float)physicalScreenSizeInMm.y);

            var bezel = new Vector2(
                normalizedViewportSubsection.width * normalizedBezel.x,
                normalizedViewportSubsection.height * normalizedBezel.y);

            return Rect.MinMaxRect(
                normalizedViewportSubsection.min.x + bezel.x,
                normalizedViewportSubsection.min.y + bezel.y,
                normalizedViewportSubsection.max.x - bezel.x,
                normalizedViewportSubsection.max.y - bezel.y);
        }

        static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }
    }
}
