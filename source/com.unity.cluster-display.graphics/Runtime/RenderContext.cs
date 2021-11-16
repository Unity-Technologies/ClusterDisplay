using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO Capitalize props.
    struct RenderContext
    {
        public int currentTileIndex;
        public int numTiles;
        public Vector2Int overscannedSize;
        public Viewport viewport;
        public AsymmetricProjection asymmetricProjection;
        public BlitParams blitParams;
        public PostEffectsParams postEffectsParams;
        // Debug data.
        public Rect debugViewportSubsection;
        public bool useDebugViewportSubsection;
    }
}
