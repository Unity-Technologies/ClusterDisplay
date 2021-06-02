using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and calls Camera.Render() for a single tile.
    /// </summary>
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose() {}

#if CLUSTER_DISPLAY_XR
        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRTHandle(width, height);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRTHandle(width, height);
#else
        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRenderTexture(width, height);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height);
#endif
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public override void LateUpdate()
        {
            if (k_ClusterRenderer.cameraController.contextCamera == null)
                return;

            var camera = k_ClusterRenderer.cameraController.contextCamera;
            if (camera.enabled)
                camera.enabled = false;

            k_ClusterRenderer.cameraController.ResetProjectionMatrix();

            if (!SetupTiledLayout(
                camera, 
                out var _, 
                out var projMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return;

            var rt = BlitRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            if (rt != camera.targetTexture)
                camera.targetTexture = rt;

            camera.projectionMatrix = projMatrix;
            camera.cullingMatrix = projMatrix * camera.worldToCameraMatrix;

            // This gets refactored later.
            k_ClusterRenderer.context.debugSettings.enableKeyword = k_ClusterRenderer.context.debugSettings.enableKeyword;
            UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

            camera.Render();

            k_ClusterRenderer.cameraController.ResetProjectionMatrix();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return;

            var scaleBias = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset); ;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            var presentRT = PresentRT((int)Screen.width, (int)Screen.height);
            var blitRT = BlitRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;

            cmd.SetRenderTarget(presentRT);
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            Blit(cmd, presentRT, blitRT, scaleBias, k_ScaleBiasRT);
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
