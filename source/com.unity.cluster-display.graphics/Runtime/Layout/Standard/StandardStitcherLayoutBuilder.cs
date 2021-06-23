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
        private Rect m_OverscannedRect;

        private StandardStitcherRTManager m_RTManager = new StandardStitcherRTManager();

        /// <summary>
        /// Where rendering actually occurs.
        /// </summary>
        public override void LateUpdate ()
        {
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var camera))
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return;

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = k_ClusterRenderer.cameraController.CacheAndReturnProjectionMatrix();

            for (var i = 0; i < numTiles; i++)
            {
                CalculateStitcherLayout(
                    camera, 
                    cachedProjectionMatrix,
                    i, 
                    ref cullingParams, 
                    out var percentageViewportSubsection, 
                    out var viewportSubsection, 
                    out var asymmetricProjectionMatrix);

                ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: k_ClusterRenderer.context.debugSettings.enableKeyword);
                UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

                var blitRT = m_RTManager.GetSourceRT(numTiles, i, (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
                CalculcateAndQueueStitcherParameters(blitRT, m_OverscannedRect, percentageViewportSubsection);

                camera.targetTexture = blitRT;
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            k_ClusterRenderer.cameraController.ResetProjectionMatrix();

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
            var presentRT = m_RTManager.GetPresentRT((int)Screen.width, (int)Screen.height);
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
            cmd.Clear();

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}
