using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and loops through each tile calling Camera.Render(), then stitches it together.
    /// </summary>
    class StandardStitcherLayoutBuilder : StitcherLayoutBuilder, ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        RenderTexture[] m_SourceRts;
        RenderTexture m_PresentRt;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        protected StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }

        public override void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }

        void ClearTiles(int numTiles)
        {
            var cmd = CommandBufferPool.Get("ClearRT");
            var overscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
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
        public override void LateUpdate()
        {
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var camera))
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;
            ClearTiles(numTiles);

            var overscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = camera.projectionMatrix;
            AllocateSourcesIfNeeded(numTiles, overscannedRect);

            for (var tileIndex = 0; tileIndex < numTiles; tileIndex++)
            {
                CalculateStitcherLayout(
                    camera,
                    cachedProjectionMatrix,
                    tileIndex,
                    out var percentageViewportSubsection,
                    out var viewportSubsection,
                    out var asymmetricProjectionMatrix);

                ClusterRenderer.ToggleClusterDisplayShaderKeywords(k_ClusterRenderer.context.debugSettings.enableKeyword);
                UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

                CalculcateAndQueueStitcherParameters(tileIndex, m_SourceRts[tileIndex], overscannedRect, percentageViewportSubsection);

                camera.targetTexture = m_SourceRts[tileIndex];
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }

        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }

        /// <summary>
        /// When were done with this frame, stitch the results together.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cameras"></param>
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!ValidGridSize(out var numTiles))
                return;

            // Once the queue reaches the number of tiles, then we perform the blit copy.
            if (m_QueuedStitcherParameters.Count < numTiles)
                return;

            var m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            cmd.SetRenderTarget(m_PresentRt);
            cmd.SetViewport(new Rect(0f, 0f, m_PresentRt.width, m_PresentRt.height));
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            for (var i = 0; i < numTiles; i++)
            {
                var croppedViewport = GraphicsUtil.TileIndexToViewportSection(k_ClusterRenderer.context.gridSize, i);
                var stitcherParameters = m_QueuedStitcherParameters.Dequeue();

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);
                var sourceRT = stitcherParameters.sourceRT as RenderTexture;

                Blit(
                    cmd,
                    sourceRT,
                    stitcherParameters.scaleBiasTex,
                    stitcherParameters.scaleBiasRT);
            }

            m_QueuedStitcherParameters.Clear();

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            k_ClusterRenderer.cameraController.presenter.presentRT = m_PresentRt;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        void AllocateSourcesIfNeeded(int numTiles, Rect overscannedRect)
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, numTiles, "Source", (int)overscannedRect.width, (int)overscannedRect.height, k_DefaultFormat);
        }
    }
}
