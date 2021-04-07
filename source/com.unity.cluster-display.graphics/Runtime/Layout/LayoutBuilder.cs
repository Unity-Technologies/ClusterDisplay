using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder : ClusterRenderer.IClusterRendererEventReceiver
    {
        public static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);
        protected readonly IClusterRenderer m_ClusterRenderer;

        public abstract ClusterRenderer.LayoutMode LayoutMode { get; }

        public LayoutBuilder (IClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;
        ~LayoutBuilder () => Dispose();
        public abstract void LateUpdate();
        public abstract void OnBeginRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndRender(ScriptableRenderContext context, Camera camera);
        public abstract void Dispose();

        protected bool ValidGridSize (out int numTiles) => (numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y) > 0;

        protected Rect CalculateOverscannedRect (int width, int height)
        {
            return new Rect(0, 0, 
                width + 2 * m_ClusterRenderer.Context.OverscanInPixels, 
                height + 2 * m_ClusterRenderer.Context.OverscanInPixels);
        }

        protected Vector2 CalculateCroppedSize (Rect rect, int overscanInPixels) => new Vector2(rect.width - 2 * overscanInPixels, rect.height - 2 * overscanInPixels);

        protected Vector4 CalculateScaleBias (Rect overscannedRect, int overscanInPixels, Vector2 debugOffset)
        {
            var croppedSize = new Vector2(overscannedRect.width - 2 * overscanInPixels, overscannedRect.height - 2 * overscanInPixels);
            var overscannedSize = new Vector2(overscannedRect.width, overscannedRect.height);

            var scaleBias = new Vector4(
                croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                overscanInPixels / overscannedSize.x, overscanInPixels / overscannedSize.y); // offset
            scaleBias.z += debugOffset.x;
            scaleBias.w += debugOffset.y;

            return scaleBias;
        }
    }
}
