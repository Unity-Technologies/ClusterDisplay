using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose() {}

        private RTHandle m_OverscannedTarget;
        private Rect m_OverscannedRect;

        protected RTHandle m_PresentTarget;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.CameraContext == null)
                return;

            if (m_ClusterRenderer.CameraController.CameraContext.enabled)
                m_ClusterRenderer.CameraController.CameraContext.enabled = false;

            m_ClusterRenderer.CameraController.CameraContext.Render();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            if (!SetupTiledLayout(
                camera, 
                ref m_OverscannedTarget,
                out var _, 
                out var projMatrix, 
                out var _,
                out m_OverscannedRect))
                return;

            UploadClusterDisplayParams(projMatrix);
            camera.projectionMatrix = projMatrix;
            camera.cullingMatrix = projMatrix * camera.worldToCameraMatrix;

            m_ClusterRenderer.CameraController.CameraContextRenderTexture = m_OverscannedTarget;

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);

            bool resized = 
                m_PresentTarget != null && 
                (m_PresentTarget.rt.width != (int)croppedSize.x || 
                m_PresentTarget.rt.height != (int)croppedSize.y);

            if (m_PresentTarget == null || resized)
            {
                if (m_PresentTarget != null)
                    RTHandles.Release(m_PresentTarget);

                m_PresentTarget = RTHandles.Alloc(
                    width: (int)croppedSize.x, 
                    height: (int)croppedSize.y,
                    slices: 1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    name: "Present Target");
            }
        }

        public virtual void Blit(CommandBuffer cmd, RTHandle target, Vector4 texBias, Vector4 rtBias) {}
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (m_ClusterRenderer.CameraController.CameraContext != camera)
                return;

            var scaleBias = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset); ;

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");
            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentTarget;

            cmd.SetRenderTarget(m_PresentTarget);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            Blit(cmd, m_OverscannedTarget, scaleBias, k_ScaleBiasRT);
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
