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
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose() {}

#if CLUSTER_DISPLAY_XR
        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRTHandle(width, height);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRTHandle(width, height);
#else
        public RenderTexture SourceRT(int width, int height) => m_RTManager.SourceRenderTexture(width, height, GraphicsFormat.B8G8R8A8_SRGB);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height, GraphicsFormat.B8G8R8A8_SRGB);
        public RenderTexture BackBufferRT(int width, int height) => m_RTManager.BackBufferRenderTexture(width, height, GraphicsFormat.B8G8R8A8_SRGB);
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
                out var projectionMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return;

            var rt = SourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            if (rt != camera.targetTexture)
                camera.targetTexture = rt;

            camera.projectionMatrix = projectionMatrix;
            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: k_ClusterRenderer.context.debugSettings.enableKeyword);
            UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, k_ClusterRenderer.context.globalScreenSize, k_ClusterRenderer.context.gridSize));

            camera.projectionMatrix = projectionMatrix;
            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

            camera.Render();

            k_ClusterRenderer.cameraController.ResetProjectionMatrix();
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
            var presentRT = PresentRT((int)Screen.width, (int)Screen.height);
            var sourceRT = SourceRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            // cmd.SetRenderTarget(presentRT);
            // cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
            // Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);

            if (ClusterDisplay.ClusterDisplayState.IsMaster)
            {
                var backBufferRT = BackBufferRT((int)Screen.width, (int)Screen.height);

                cmd.SetRenderTarget(presentRT);
                // cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
                Blit(cmd, backBufferRT, new Vector4(1, 1, 0, 0), new Vector4(1, 1, 0, 0));

                cmd.SetRenderTarget(backBufferRT);
                // cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
            }

            else
            {
                cmd.SetRenderTarget(presentRT);
                cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
                Blit(cmd, sourceRT, texBias, k_ScaleBiasRT);
            }

            k_ClusterRenderer.cameraController.presenter.presentRT = presentRT;
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
