using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose() {}

#if CLUSTER_DISPLAY_XR
        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRTHandle(width, height);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRTHandle(width, height);
#else
        public RenderTexture BlitRT(int width, int height) => m_RTManager.BlitRenderTexture(width, height, GraphicsFormat.R8G8B8A8_UNorm);
        public RenderTexture PresentRT(int width, int height) => m_RTManager.PresentRenderTexture(width, height, GraphicsFormat.R8G8B8A8_UNorm);
#endif
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public override void LateUpdate()
        {
            var camera = m_ClusterRenderer.CameraController.ContextCamera;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;
            camera.usePhysicalProperties = true;

            if (!SetupTiledLayout(
                camera, 
                out var _, 
                out var projectionMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return;

            var rt = BlitRT((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            if (rt != camera.targetTexture)
                camera.targetTexture = rt;

            m_ClusterRenderer.CameraController.CacheContextProjectionMatrix();
            camera.projectionMatrix = projectionMatrix;
            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: m_ClusterRenderer.Context.DebugSettings.EnableKeyword);
            UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_ClusterRenderer.Context.GlobalScreenSize, m_ClusterRenderer.Context.GridSize));

            camera.Render();

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(keywordEnabled: false);
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
            cmd.ClearRenderTarget(true, true, m_ClusterRenderer.Context.Debug ? m_ClusterRenderer.Context.BezelColor : Color.black);

            Blit(cmd, blitRT, scaleBias, k_ScaleBiasRT);
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
