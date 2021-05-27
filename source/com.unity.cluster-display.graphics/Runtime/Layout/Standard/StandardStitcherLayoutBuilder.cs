using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and loops through each tile calling Camera.Render(), then stitches it together.
    /// </summary>
    public class StandardStitcherLayoutBuilder : StitcherLayoutBuilder, ILayoutBuilder
    {
        public StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardStitcher;
        private StandardStitcherRTManager m_RTManager = new StandardStitcherRTManager();
        private RenderTexture BlitRT(int tileCount, int tileIdnex, int width, int height) => m_RTManager.BlitRenderTexture(tileCount, tileIdnex, width, height);
        private RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height);
        private Rect m_OverscannedRect;

        /// <summary>
        /// Where rendering actually occurs.
        /// </summary>
        public override void LateUpdate ()
        {
            var camera = k_ClusterRenderer.cameraController.contextCamera;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return;

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);

            for (var i = 0; i < numTiles; i++)
            {
                k_ClusterRenderer.cameraController.CacheContextProjectionMatrix();
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

                camera.projectionMatrix = projectionMatrix;
                camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

                UploadClusterDisplayParams(projectionMatrix);
                camera.Render();

                k_ClusterRenderer.cameraController.ApplyCachedProjectionMatrixToContext();
            }

            /*
            if (camera.enabled)
                camera.enabled = cameraEnabled;
            */
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}

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

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            var presentRT = PresentRT((int)Screen.width, (int)Screen.height);
            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");
            cmd.SetRenderTarget(k_ClusterRenderer.cameraController.presenter.presentRT);
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            for (var i = 0; i < numTiles; i++)
            {
                Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(k_ClusterRenderer.context.gridSize, i);
                var stitcherParameters = m_QueuedStitcherParameters.Dequeue();

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);

                Blit(
                    cmd, 
                    k_ClusterRenderer.cameraController.presenter.presentRT, 
                    stitcherParameters.targetRT as RenderTexture, 
                    stitcherParameters.scaleBiasTex, 
                    stitcherParameters.scaleBiasRT);
            }

            m_QueuedStitcherParameters.Clear();
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}
