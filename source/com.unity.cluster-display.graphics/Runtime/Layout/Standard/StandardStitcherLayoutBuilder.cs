using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardStitcherLayoutBuilder : StitcherLayoutBuilder, ILayoutBuilder
    {
        public StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}


        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        private RenderTexture BlitRT(int tileCount, int tileIdnex, int width, int height) => m_RTManager.BlitRenderTexture(tileCount, tileIdnex, width, height);
        private RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height); 
        private Rect m_OverscannedRect;

        public override void LateUpdate ()
        {
            var camera = m_ClusterRenderer.CameraController.CameraContext;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;

            Assert.IsTrue(m_QueuedStitcherParameters.Count == 0);

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return;

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);

            for (var i = 0; i < numTiles; i++)
            {
                CalculateStitcherLayout(
                    camera, 
                    i, 
                    ref cullingParams, 
                    out var percentageViewportSubsection, 
                    out var _, 
                    out var projectionMatrix);


                var blitRT = BlitRT(numTiles, i, (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
                CalculcateAndQueueStitcherParameters(blitRT, m_OverscannedRect, percentageViewportSubsection);
                camera.targetTexture = blitRT;

                m_ClusterRenderer.CameraController.CacheContextProjectionMatrix();

                camera.projectionMatrix = projectionMatrix;
                camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

                UploadClusterDisplayParams(projectionMatrix);
                camera.Render();

                m_ClusterRenderer.CameraController.ApplyCachedProjectionMatrixToContext();
            }
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!m_ClusterRenderer.CameraController.CameraIsInContext(camera))
                return;

            if (!ValidGridSize(out var numTiles))
                return;

            // Once the queue reaches the number of tiles, then we perform the blit copy.
            if (m_QueuedStitcherParameters.Count < numTiles)
                return;

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
            var presentRT = PresentRT((int)Screen.width, (int)Screen.height);
            m_ClusterRenderer.CameraController.Presenter.PresentRT = presentRT;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");
            cmd.SetRenderTarget(m_ClusterRenderer.CameraController.Presenter.PresentRT);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            for (var i = 0; i < numTiles; i++)
            {
                Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_ClusterRenderer.Context.GridSize, i);
                var stitcherParameters = m_QueuedStitcherParameters.Dequeue();

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);

                Blit(
                    cmd, 
                    m_ClusterRenderer.CameraController.Presenter.PresentRT, 
                    stitcherParameters.targetRT as RenderTexture, 
                    stitcherParameters.scaleBiasTex, 
                    stitcherParameters.scaleBiasRT);
            }

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
