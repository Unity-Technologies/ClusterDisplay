﻿using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder : ClusterRenderer.IClusterRendererEventReceiver
    {
        public static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);
        public static readonly string k_ClusterDisplayParamsShaderVariableName = "_ClusterDisplayParams";
        protected readonly IClusterRenderer m_ClusterRenderer;

        public abstract ClusterRenderer.LayoutMode LayoutMode { get; }

        public LayoutBuilder (IClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;
        public abstract void LateUpdate();
        public abstract void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void Dispose();

        protected bool ValidGridSize (out int numTiles) => (numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y) > 0;
        public void UploadClusterDisplayParams (Matrix4x4 projectionMatrix) => Shader.SetGlobalMatrix(k_ClusterDisplayParamsShaderVariableName, /*GL.GetGPUProjectionMatrix(projectionMatrix, true)*/projectionMatrix);

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

        private static MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();
        protected void Blit (CommandBuffer cmd, RenderTexture presentRT, RenderTexture target, Vector4 texBias, Vector4 rtBias)
        {
            s_PropertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), target);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            s_PropertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, m_ClusterRenderer.Settings.Resources.BlitMaterial, 0, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }
    }
}
