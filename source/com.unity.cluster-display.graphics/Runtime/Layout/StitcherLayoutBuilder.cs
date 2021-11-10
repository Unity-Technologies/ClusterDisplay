using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class StitcherLayoutBuilder : ILayoutBuilder
    {
        struct StitcherParameters
        {
            public object SourceRT;
            public Vector4 ScaleBiasTex;
            public Vector4 ScaleBiasRT;
        }

        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderContext m_Context;
        RenderTexture[] m_SourceRts;
        RenderTexture m_PresentRt;

        Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        public LayoutMode LayoutMode => LayoutMode.StandardStitcher;

        public RenderTexture PresentRT => m_PresentRt;

        public StitcherLayoutBuilder(ClusterRenderContext context) => m_Context = context;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }

        void ClearTiles(int numTiles)
        {
            var cmd = CommandBufferPool.Get("ClearRT");
            var overscannedRect = LayoutBuilderUtils.CalculateOverscannedRect(Screen.width, Screen.height, m_Context.OverscanInPixels);
            AllocateSourcesIfNeeded(numTiles, overscannedRect);

            for (var i = 0; i < numTiles; i++)
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
        public void Render(Camera camera)
        {

            var numTiles = m_Context.GridSize.y * m_Context.GridSize.x;
            
            ClearTiles(numTiles);

            var overscannedRect = LayoutBuilderUtils.CalculateOverscannedRect(Screen.width, Screen.height, m_Context.OverscanInPixels);
            var cachedProjectionMatrix = camera.projectionMatrix;
            AllocateSourcesIfNeeded(numTiles, overscannedRect);

            for (var tileIndex = 0; tileIndex < numTiles; tileIndex++)
            {
                LayoutBuilderUtils.GetViewportAndProjection(
                    m_Context,
                    cachedProjectionMatrix,
                    tileIndex,
                    out var asymmetricProjectionMatrix,
                    out var viewportSubsection);

                    LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_Context.GlobalScreenSize, m_Context.GridSize));

                CalculateAndQueueStitcherParameters(tileIndex, m_SourceRts[tileIndex], overscannedRect);

                camera.aspect = m_Context.GridSize.x * Screen.width / (float)(m_Context.GridSize.y * Screen.height);
                camera.targetTexture = m_SourceRts[tileIndex];
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            camera.ResetAspect();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }
        
        public void Present()
        {
            var numTiles = m_Context.GridSize.y * m_Context.GridSize.x;

            // Should be invoked once all tiles were rendered and enqueued.
            Assert.IsTrue(m_QueuedStitcherParameters.Count == numTiles);

            var overscannedRect = LayoutBuilderUtils.CalculateOverscannedRect(Screen.width, Screen.height, m_Context.OverscanInPixels);
            var croppedSize = LayoutBuilderUtils.CalculateCroppedSize(overscannedRect, m_Context.OverscanInPixels);
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            cmd.SetRenderTarget(m_PresentRt);
            cmd.SetViewport(new Rect(0f, 0f, m_PresentRt.width, m_PresentRt.height));
            cmd.ClearRenderTarget(true, true, m_Context.Debug ? m_Context.BezelColor : Color.black);

            for (var i = 0; i < numTiles; i++)
            {
                var croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_Context.GridSize, i);
                var stitcherParameters = m_QueuedStitcherParameters.Dequeue();

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);
                var sourceRT = stitcherParameters.SourceRT as RenderTexture;

                GraphicsUtil.Blit(cmd, sourceRT, stitcherParameters.ScaleBiasTex, stitcherParameters.ScaleBiasRT);
            }

            m_QueuedStitcherParameters.Clear();

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        void AllocateSourcesIfNeeded(int numTiles, Rect overscannedRect)
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, numTiles, "Source", (int)overscannedRect.width, (int)overscannedRect.height, k_DefaultFormat);
        }

        void CalculateAndQueueStitcherParameters<T>(int tileIndex, T targetRT, Rect overscannedRect)
        {
            var scaleBiasTex = LayoutBuilderUtils.CalculateScaleBias(overscannedRect, m_Context.OverscanInPixels, m_Context.DebugScaleBiasTexOffset);
            var croppedSize = LayoutBuilderUtils.CalculateCroppedSize(overscannedRect, m_Context.OverscanInPixels);

            var scaleBiasRT = new Vector4(
                1 - m_Context.Bezel.x * 2 / croppedSize.x, 1 - m_Context.Bezel.y * 2 / croppedSize.y, // scale
                m_Context.Bezel.x / croppedSize.x, m_Context.Bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                ScaleBiasTex = scaleBiasTex,
                ScaleBiasRT = scaleBiasRT,
                SourceRT = targetRT
            });
        }
    }
}
