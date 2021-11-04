using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    abstract class LayoutBuilder : IClusterRendererEventReceiver
    {
        protected static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);
        protected static readonly string k_ClusterDisplayParamsShaderVariableName = "_ClusterDisplayParams";
        protected readonly IClusterRenderer k_ClusterRenderer;

        public abstract ClusterRenderer.LayoutMode layoutMode { get; }

        public LayoutBuilder(IClusterRenderer clusterRenderer)
        {
            k_ClusterRenderer = clusterRenderer;
        }

        public abstract void LateUpdate();
        public abstract void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void Dispose();

        protected bool ValidGridSize(out int numTiles)
        {
            return (numTiles = k_ClusterRenderer.context.gridSize.x * k_ClusterRenderer.context.gridSize.y) > 0;
        }

        public void UploadClusterDisplayParams(Matrix4x4 clusterDisplayParams)
        {
            Shader.SetGlobalMatrix(k_ClusterDisplayParamsShaderVariableName, clusterDisplayParams);
        }

        protected Rect CalculateOverscannedRect(int width, int height)
        {
            return new Rect(0, 0,
                width + 2 * k_ClusterRenderer.context.overscanInPixels,
                height + 2 * k_ClusterRenderer.context.overscanInPixels);
        }

        protected Vector2 CalculateCroppedSize(Rect rect, int overscanInPixels)
        {
            return new Vector2(rect.width - 2 * overscanInPixels, rect.height - 2 * overscanInPixels);
        }

        protected Vector4 CalculateScaleBias(Rect overscannedRect, int overscanInPixels, Vector2 debugOffset)
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
