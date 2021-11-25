using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO Capitalize props.
    ref struct RenderContext
    {
        public int currentTileIndex;
        public int numTiles;
        public Vector2Int overscannedSize;
        public Viewport viewport;
        public Matrix4x4 originalProjection;
        public BlitParams blitParams;
        public PostEffectsParams postEffectsParams;
        // Debug data.
        public Rect debugViewportSubsection;
        public bool useDebugViewportSubsection;
    }
}
