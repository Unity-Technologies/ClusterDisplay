using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and calls Camera.Render() for a single tile.
    /// </summary>
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        private StandardTileRTManager m_RTManager = new StandardTileRTManager();
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardTile;
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        public override void LateUpdate()
        {
            if (!ClusterCameraController.TryGetContextCamera(out var camera))
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!SetupTiledLayout(
                camera, 
                out var asymmetricProjectionMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return;

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: k_ClusterRenderer.context.debugSettings.enableKeyword);
            UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

            camera.targetTexture = m_RTManager.GetSourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            camera.projectionMatrix = asymmetricProjectionMatrix;
            camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

            camera.Render();
            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");
            cmd.Clear();

            Vector4 texBias = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            var presentRT = m_RTManager.GetPresentRT((int)Screen.width, (int)Screen.height);
            var sourceRT =  m_RTManager.GetSourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            if (ClusterDisplay.ClusterDisplayState.IsEmitter)
            {
                var backBufferRT = m_RTManager.GetBackBufferRT((int)Screen.width, (int)Screen.height);

                cmd.SetRenderTarget(presentRT);
                Blit(cmd, backBufferRT, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0));

                cmd.SetRenderTarget(backBufferRT);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
            }

            else
            {
                cmd.SetRenderTarget(presentRT);
                cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
            }

            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;

            Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
