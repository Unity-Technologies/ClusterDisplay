using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    struct RenderContext
    {
        public int currentTileIndex;
        public int numTiles;
        public Vector2Int overscannedSize;
        // TODO used for aspect, remove?
        public Vector2Int displayMatrixSize;
        public Viewport viewport;
        public AsymmetricProjection asymmetricProjection;
        public BlitParams blitParams;
        public PostEffectsParams postEffectsParams;
    }
}
