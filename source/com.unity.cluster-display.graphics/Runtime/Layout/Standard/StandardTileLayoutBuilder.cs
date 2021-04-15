using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose() {}

        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRenderTexture(width, height);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height);
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.ContextCamera == null)
                return;

            if (m_ClusterRenderer.CameraController.ContextCamera.enabled)
                m_ClusterRenderer.CameraController.ContextCamera.enabled = false;

            var camera = m_ClusterRenderer.CameraController.ContextCamera;

            if (!SetupTiledLayout(
                camera, 
                out var _, 
                out var projMatrix, 
                out var _,
                out m_OverscannedRect))
                return;

            var rt = BlitRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            if (rt != camera.targetTexture)
                camera.targetTexture = rt;

            UploadClusterDisplayParams(projMatrix);

            m_ClusterRenderer.CameraController.CacheContextProjectionMatrix();
            camera.projectionMatrix = projMatrix;
            camera.cullingMatrix = projMatrix * camera.worldToCameraMatrix;

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);

            camera.Render();
            m_ClusterRenderer.CameraController.ApplyCachedProjectionMatrixToContext();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!m_ClusterRenderer.CameraController.CameraIsInContext(camera))
                return;

            var scaleBias = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset); ;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            var presentRT = PresentRT((int)Screen.width, (int)Screen.height);
            var blitRT = BlitRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            m_ClusterRenderer.CameraController.Presenter.PresentRT = presentRT;

            cmd.SetRenderTarget(presentRT);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            Blit(cmd, presentRT, blitRT, scaleBias, k_ScaleBiasRT);
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
