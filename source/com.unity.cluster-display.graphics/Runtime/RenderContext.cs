using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    ref struct RenderContext
    {
        public int CurrentTileIndex;
        public int NumTiles;
        public Vector2Int OverscannedSize;
        public Viewport Viewport;
        public Matrix4x4 OriginalProjection;
        public BlitParams BlitParams;
        public PostEffectsParams PostEffectsParams;
        // Debug data.
        public Rect DebugViewportSubsection;
        public bool UseDebugViewportSubsection;
    }
}
