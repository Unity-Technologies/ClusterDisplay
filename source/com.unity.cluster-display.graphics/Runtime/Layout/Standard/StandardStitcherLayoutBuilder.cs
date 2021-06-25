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
        private StandardStitcherRTManager m_RTManager = new StandardStitcherRTManager();
        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        public StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        private void ClearTiles (int numTiles)
        {
            var cmd = CommandBufferPool.Get("ClearRT");

            var m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            for (var i = 0; i < numTiles; i++)
            {
                var sourceRT = m_RTManager.GetSourceRT(numTiles, i, (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
                cmd.SetRenderTarget(sourceRT);
                cmd.ClearRenderTarget(true, true, Color.black);
            }

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

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
            ClearTiles(numTiles);

            var m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = camera.projectionMatrix;

            for (var tileIndex = 0; tileIndex < numTiles; tileIndex++)
            {
                CalculateStitcherLayout(
                    camera, 
                    cachedProjectionMatrix,
                    tileIndex, 
                    out var percentageViewportSubsection, 
                    out var viewportSubsection, 
                    out var asymmetricProjectionMatrix);

                ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: k_ClusterRenderer.context.debugSettings.enableKeyword);
                UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

                var sourceRT = m_RTManager.GetSourceRT(numTiles, tileIndex, (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
                CalculcateAndQueueStitcherParameters(tileIndex, sourceRT, m_OverscannedRect, percentageViewportSubsection);

                camera.targetTexture = sourceRT;
                camera.projectionMatrix = asymmetricProjectionMatrix;
                camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) 
        {
        }

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

            var m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            var presentRT = m_RTManager.GetPresentRT((int)Screen.width, (int)Screen.height);

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            cmd.SetRenderTarget(presentRT);
            cmd.SetViewport(new Rect(0f, 0f, presentRT.width, presentRT.height));
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

            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
}
