using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class TileLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderContext m_Context;
        Rect m_OverscannedRect;
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
        
        public void Render(Camera camera)
        {
            m_OverscannedRect = LayoutBuilderUtils.CalculateOverscannedRect(Screen.width, Screen.height, m_Context.OverscanInPixels);

            // TODO implicit tile index is confusing?
            LayoutBuilderUtils.GetViewportAndProjection(
                m_Context,
                camera.projectionMatrix,
                -1,
                out var asymmetricProjectionMatrix,
                out var viewportSubsection);

            LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_Context.GlobalScreenSize, m_Context.GridSize));

            AllocateSourceIfNeeded();
            camera.aspect = m_Context.GridSize.x * Screen.width / (float)(m_Context.GridSize.y * Screen.height);
            camera.targetTexture = m_SourceRt;
            camera.projectionMatrix = asymmetricProjectionMatrix;
            camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

            camera.Render();

            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public void Present(CommandBuffer commandBuffer)
        {
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);
            commandBuffer.SetRenderTarget(m_PresentRt);
            commandBuffer.ClearRenderTarget(true, true, m_Context.Debug ? m_Context.BezelColor : Color.black);

            var scaleBias = LayoutBuilderUtils.CalculateScaleBias(m_OverscannedRect, m_Context.OverscanInPixels, m_Context.DebugScaleBiasTexOffset);

            AllocateSourceIfNeeded();
            GraphicsUtil.Blit(commandBuffer, m_SourceRt, scaleBias, LayoutBuilderUtils.ScaleBiasRT);
        }
        
        void AllocateSourceIfNeeded()
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRt, "Source", (int)m_OverscannedRect.width, (int)m_OverscannedRect.height, k_DefaultFormat);
        }
    }
}
