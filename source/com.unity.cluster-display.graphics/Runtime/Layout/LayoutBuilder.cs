using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class LayoutBuilder : ClusterRenderer.IClusterRendererEventReceiver
    {
        public static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);
        public static readonly string k_ClusterDisplayParamsShaderVariableName = "_ClusterDisplayParams";

        /// <summary>
        /// Interfaced handle to ClusterRenderer class that initializes this instance.
        /// </summary>
        protected readonly IClusterRenderer k_ClusterRenderer;

        public abstract ClusterRenderer.LayoutMode layoutMode { get; }

        public LayoutBuilder (IClusterRenderer clusterRenderer) => k_ClusterRenderer = clusterRenderer;
        public abstract void LateUpdate();
        public abstract void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void OnBeginCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndCameraRender(ScriptableRenderContext context, Camera camera);
        public abstract void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras);
        public abstract void Dispose();

        /// <summary>
        /// Deteremine whether the grid size is valid and return 
        /// the number of tiles if it is valid.
        /// </summary>
        /// <param name="numTiles">The output number of tiles in the grid.</param>
        /// <returns></returns>
        protected bool ValidGridSize (out int numTiles) => (numTiles = k_ClusterRenderer.context.gridSize.x * k_ClusterRenderer.context.gridSize.y) > 0;

        /// <summary>
        /// Upload cluster display parameters for use in ClusterDisplay.hlsl
        /// </summary>
        /// <param name="clusterDisplayParams">Parameters used for post processing compatible with cluster display.</param>
        public void UploadClusterDisplayParams (Matrix4x4 clusterDisplayParams) => Shader.SetGlobalMatrix(k_ClusterDisplayParamsShaderVariableName, clusterDisplayParams);

        /// <summary>
        /// After we render at the screen resolution + the overscan for post processing, we crop down to the actual render
        /// size to provide seamless tiling across the cluster.
        /// </summary>
        /// <param name="rect">This should be (resolution + overscan resolution).</param>
        /// <param name="overscanInPixels">The number of pixels that border the presented image for post processing.</param>
        /// <returns></returns>
        protected Vector2 CalculateCroppedSize (Rect rect, int overscanInPixels) => new Vector2(rect.width - 2 * overscanInPixels, rect.height - 2 * overscanInPixels);

        /// <summary>
        /// Calculate the Vector4 used to crop the source RT and copy the cropped pixels into a target RT.
        /// </summary>
        /// <param name="overscannedRect">This should be (resolution + overscan resolution).</param>
        /// <param name="overscanInPixels">The number of pixels that border the presented image for post processing.</param>
        /// <param name="debugOffset">Used to shift the crop for debugging purposes.</param>
        /// <returns></returns>
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

        /// <summary>
        /// This was refactored out of HDRP's HDUtils.Blit into ClusterDisplay
        /// so it can be used across both HDRP or URP.
        /// </summary>
        /// <param name="cmd">CommandBuffer to use.</param>
        /// <param name="source">The render texture to copy from.</param>
        /// <param name="texBias">Crop source.</param>
        /// <param name="rtBias">Crop target.</param>
        protected void Blit (CommandBuffer cmd, RenderTexture source, Vector4 texBias, Vector4 rtBias)
        {
            s_PropertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), source);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            s_PropertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);
            cmd.DrawProcedural(Matrix4x4.identity, k_ClusterRenderer.settings.resources.blitMaterial, 0, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }

        protected void Blit (CommandBuffer cmd, RTHandle source, Vector4 texBias, Vector4 rtBias)
        {
            s_PropertyBlock.SetTexture(Shader.PropertyToID("_BlitTexture"), source);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBias"), texBias);
            s_PropertyBlock.SetVector(Shader.PropertyToID("_BlitScaleBiasRt"), rtBias);
            s_PropertyBlock.SetFloat(Shader.PropertyToID("_BlitMipLevel"), 0);

            // Draw full screen quad.
            cmd.DrawProcedural(Matrix4x4.identity, k_ClusterRenderer.settings.resources.blitMaterial, 0, MeshTopology.Quads, 4, 1, s_PropertyBlock);
        }
    }
}
