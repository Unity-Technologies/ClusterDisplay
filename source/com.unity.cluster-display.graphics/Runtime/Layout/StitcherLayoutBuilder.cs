using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class StitcherLayoutBuilder : ILayoutBuilder
    {
        struct StitcherParameters
        {
            public int TileIndex;
            public object SourceRT;
            public Vector4 ScaleBiasTex;
            public Vector4 ScaleBiasRT;
        }

        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly IClusterRenderer m_ClusterRenderer;
        RenderTexture[] m_SourceRts;
        RenderTexture m_PresentRt;

        Queue<StitcherParameters> m_QueuedStitcherParameters = new Queue<StitcherParameters>();

        public ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        public StitcherLayoutBuilder(IClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }

        void ClearTiles(int numTiles)
        {
            var cmd = CommandBufferPool.Get("ClearRT");
            var overscannedRect = m_ClusterRenderer.CalculateOverscannedRect(Screen.width, Screen.height);
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
        public void LateUpdate()
        {
            if (!m_ClusterRenderer.CameraController.TryGetContextCamera(out var camera))
            {
                return;
            }

            if (camera.enabled)
            {
                camera.enabled = false;
            }

            if (!m_ClusterRenderer.ValidGridSize(out var numTiles))
            {
                return;
            }

            ClearTiles(numTiles);

            var overscannedRect = m_ClusterRenderer.CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = camera.projectionMatrix;
            AllocateSourcesIfNeeded(numTiles, overscannedRect);

            for (var tileIndex = 0; tileIndex < numTiles; tileIndex++)
            {
                CalculateStitcherLayout(
                    cachedProjectionMatrix,
                    tileIndex,
                    out var percentageViewportSubsection,
                    out var viewportSubsection,
                    out var asymmetricProjectionMatrix);

                ClusterRenderer.ToggleClusterDisplayShaderKeywords(m_ClusterRenderer.Context.DebugSettings.EnableKeyword);
                LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_ClusterRenderer.Context.GlobalScreenSize, m_ClusterRenderer.Context.GridSize));

                CalculcateAndQueueStitcherParameters(tileIndex, m_SourceRts[tileIndex], overscannedRect, percentageViewportSubsection);

                camera.targetTexture = m_SourceRts[tileIndex];
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }
        
        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }

        public void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }
        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }

        /// <summary>
        /// When were done with this frame, stitch the results together.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cameras"></param>
        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!m_ClusterRenderer.ValidGridSize(out var numTiles))
            {
                return;
            }

            // Once the queue reaches the number of tiles, then we perform the blit copy.
            if (m_QueuedStitcherParameters.Count < numTiles)
            {
                return;
            }

            var overscannedRect = m_ClusterRenderer.CalculateOverscannedRect(Screen.width, Screen.height);
            var croppedSize = LayoutBuilderUtils.CalculateCroppedSize(overscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            cmd.SetRenderTarget(m_PresentRt);
            cmd.SetViewport(new Rect(0f, 0f, m_PresentRt.width, m_PresentRt.height));
            cmd.ClearRenderTarget(true, true, m_ClusterRenderer.Context.Debug ? m_ClusterRenderer.Context.BezelColor : Color.black);

            for (var i = 0; i < numTiles; i++)
            {
                var croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_ClusterRenderer.Context.GridSize, i);
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

            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentRt;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        void AllocateSourcesIfNeeded(int numTiles, Rect overscannedRect)
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, numTiles, "Source", (int)overscannedRect.width, (int)overscannedRect.height, k_DefaultFormat);
        }

        protected void CalculcateAndQueueStitcherParameters<T>(int tileIndex, T targetRT, Rect overscannedRect, Rect percentageViewportSubsection)
        {
            var scaleBiasTex = LayoutBuilderUtils.CalculateScaleBias(overscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
            var croppedSize = LayoutBuilderUtils.CalculateCroppedSize(overscannedRect, m_ClusterRenderer.Context.OverscanInPixels);

            var scaleBiasRT = new Vector4(
                1 - m_ClusterRenderer.Context.Bezel.x * 2 / croppedSize.x, 1 - m_ClusterRenderer.Context.Bezel.y * 2 / croppedSize.y, // scale
                m_ClusterRenderer.Context.Bezel.x / croppedSize.x, m_ClusterRenderer.Context.Bezel.y / croppedSize.y); // offset

            m_QueuedStitcherParameters.Enqueue(new StitcherParameters
            {
                TileIndex = tileIndex,
                ScaleBiasTex = scaleBiasTex,
                ScaleBiasRT = scaleBiasRT,
                SourceRT = targetRT
            });
        }

        protected void CalculateStitcherLayout(
            Matrix4x4 cameraProjectionMatrix,
            int tileIndex,
            out Rect percentageViewportSubsection,
            out Rect viewportSubsection,
            out Matrix4x4 asymmetricProjectionMatrix)
        {
            percentageViewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection(tileIndex);
            viewportSubsection = percentageViewportSubsection;
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
            {
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            }

            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(cameraProjectionMatrix, viewportSubsection);
        }
    }
}
