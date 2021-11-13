using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class StitcherLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderContext m_Context;
        RenderTexture[] m_SourceRts;
        RenderTexture m_PresentRt;

        public LayoutMode LayoutMode => LayoutMode.StandardStitcher;

        public RenderTexture PresentRT => m_PresentRt;

        public StitcherLayoutBuilder(ClusterRenderContext context) => m_Context = context;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }

        void ClearTiles()
        {
            var cmd = CommandBufferPool.Get("Clear Sources");
            for (var i = 0; i != m_SourceRts.Length; ++i)
            {
                cmd.SetRenderTarget(m_SourceRts[i]);
                cmd.ClearRenderTarget(true, true, Color.black);
            }

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        /// <summary>
        /// Where rendering actually occurs.
        /// </summary>
        public void Render(Camera camera, int screenWidth, int screenHeight)
        {
            var numTiles = m_Context.GridSize.y * m_Context.GridSize.x;

            // Aspect must be updated before we pull the projection matrix.
            camera.aspect = m_Context.GetAspect(screenWidth, screenHeight);
            var cachedProjectionMatrix = camera.projectionMatrix;

            var overscannedSize = LayoutBuilderUtils.CalculateOverscannedSize(screenWidth, screenHeight, m_Context.OverscanInPixels);
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, numTiles, "Source", (int)overscannedSize.x, (int)overscannedSize.y, k_DefaultFormat);

            // TODO Is it needed?
            ClearTiles();

            for (var tileIndex = 0; tileIndex != numTiles; ++tileIndex)
            {
                LayoutBuilderUtils.GetViewportAndProjection(
                    m_Context,
                    cachedProjectionMatrix,
                    tileIndex,
                    out var asymmetricProjectionMatrix,
                    out var viewportSubsection);

                LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_Context.GlobalScreenSize, m_Context.GridSize));

                camera.targetTexture = m_SourceRts[tileIndex];
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public void Present(CommandBuffer commandBuffer, int screenWidth, int screenHeight)
        {
            var numTiles = m_Context.GridSize.y * m_Context.GridSize.x;

            // No render happened, or grid size changed, cannot present.
            if (m_SourceRts == null || m_SourceRts.Length != numTiles)
            {
                return;
            }

            var croppedSize = new Vector2(screenWidth, screenHeight);
            var overscannedSize = LayoutBuilderUtils.CalculateOverscannedSize(screenWidth, screenHeight, m_Context.OverscanInPixels);
            var scaleBiasTex = LayoutBuilderUtils.CalculateScaleBias(overscannedSize, m_Context.OverscanInPixels, m_Context.DebugScaleBiasTexOffset);
  
            var bezel = m_Context.Bezel;
            var physicalScreenSize = m_Context.PhysicalScreenSize;
            var scaleBiasRT = new Vector4(
                (physicalScreenSize.x - bezel.x * 2) / physicalScreenSize.x, 
                (physicalScreenSize.y - bezel.y * 2) / physicalScreenSize.y, 
                bezel.x / physicalScreenSize.x, bezel.y / physicalScreenSize.y); // offset
            
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", screenWidth, screenHeight, k_DefaultFormat);

            commandBuffer.SetRenderTarget(m_PresentRt);
            // TODO Is this needed?
            commandBuffer.SetViewport(new Rect(0f, 0f, m_PresentRt.width, m_PresentRt.height));
            commandBuffer.ClearRenderTarget(true, true, m_Context.Debug ? m_Context.BezelColor : Color.black);

            for (var tileIndex = 0; tileIndex != numTiles; ++tileIndex)
            {
                var croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_Context.GridSize, tileIndex);

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                commandBuffer.SetViewport(croppedViewport);

                GraphicsUtil.Blit(commandBuffer, m_SourceRts[tileIndex], scaleBiasTex, scaleBiasRT);
            }
        }
    }
}
