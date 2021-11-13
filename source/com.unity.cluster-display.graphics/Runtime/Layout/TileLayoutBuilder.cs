using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class TileLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderContext m_Context;
        RenderTexture m_SourceRt;
        RenderTexture m_PresentRt;

        public LayoutMode LayoutMode => LayoutMode.StandardTile;

        public RenderTexture PresentRT => m_PresentRt;

        public TileLayoutBuilder(ClusterRenderContext context) => m_Context = context;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRt);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }
        
        public void Render(Camera camera, int screenWidth, int screenHeight)
        {
            // Aspect must be updated before we pull the projection matrix.
            camera.aspect = m_Context.GetAspect(screenWidth, screenHeight);

            LayoutBuilderUtils.GetViewportAndProjection(
                m_Context,
                camera.projectionMatrix,
                m_Context.TileIndex,
                out var asymmetricProjectionMatrix,
                out var viewportSubsection);

            LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_Context.GlobalScreenSize, m_Context.GridSize));

            var overscannedSize = LayoutBuilderUtils.CalculateOverscannedSize(screenWidth, screenHeight, m_Context.OverscanInPixels);
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRt, "Source", (int)overscannedSize.x, (int)overscannedSize.y, k_DefaultFormat);

            camera.targetTexture = m_SourceRt;
            camera.projectionMatrix = asymmetricProjectionMatrix;
            camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

            camera.Render();

            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public void Present(CommandBuffer commandBuffer, int screenWidth, int screenHeight)
        {
            // No render happened, cannot present.
            if (m_SourceRt == null)
            {
                return;
            }
            
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", screenWidth, screenHeight, k_DefaultFormat);
            commandBuffer.SetRenderTarget(m_PresentRt);
            commandBuffer.ClearRenderTarget(true, true, m_Context.Debug ? m_Context.BezelColor : Color.black);

            var scaleBias = LayoutBuilderUtils.CalculateScaleBias(new Vector2(m_SourceRt.width, m_SourceRt.height), m_Context.OverscanInPixels, m_Context.DebugScaleBiasTexOffset);

            GraphicsUtil.Blit(commandBuffer, m_SourceRt, scaleBias);
        }
    }
}
