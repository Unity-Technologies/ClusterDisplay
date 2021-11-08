using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    class TileLayoutBuilder : ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;

        readonly ClusterRenderer m_ClusterRenderer;
        Rect m_OverscannedRect;
        RenderTexture m_SourceRt;
        RenderTexture m_PresentRt;

        public ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public TileLayoutBuilder(ClusterRenderer clusterRenderer) => m_ClusterRenderer = clusterRenderer;

        public void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRt);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }
        
        public void Update()
        {
            if (!m_ClusterRenderer.CameraController.TryGetContextCamera(out var camera))
            {
                return;
            }

            if (camera.enabled)
            {
                camera.enabled = false;
            }

            if (!SetupTiledLayout(
                camera,
                out var asymmetricProjectionMatrix,
                out var viewportSubsection,
                out m_OverscannedRect))
            {
                return;
            }

            ClusterRenderer.ToggleClusterDisplayShaderKeywords(m_ClusterRenderer.Context.DebugSettings.EnableKeyword);
            LayoutBuilderUtils.UploadClusterDisplayParams(GraphicsUtil.GetClusterDisplayParams(viewportSubsection, m_ClusterRenderer.Context.GlobalScreenSize, m_ClusterRenderer.Context.GridSize));

            AllocateSourceIfNeeded();
            camera.targetTexture = m_SourceRt;
            camera.projectionMatrix = asymmetricProjectionMatrix;
            camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

            camera.Render();

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        public void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }

        public void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!m_ClusterRenderer.CameraController.CameraIsInContext(camera))
            {
                return;
            }

            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);
            cmd.SetRenderTarget(m_PresentRt);
            cmd.ClearRenderTarget(true, true, m_ClusterRenderer.Context.Debug ? m_ClusterRenderer.Context.BezelColor : Color.black);

            var scaleBias = LayoutBuilderUtils.CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);

            AllocateSourceIfNeeded();
            GraphicsUtil.Blit(cmd, m_SourceRt, scaleBias, LayoutBuilderUtils.ScaleBiasRT);

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentRt;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        
        bool SetupTiledLayout(
            Camera camera,
            out Matrix4x4 asymmetricProjectionMatrix,
            out Rect viewportSubsection,
            out Rect overscannedRect)
        {
            if (!m_ClusterRenderer.ValidGridSize(out _) || camera.cameraType != CameraType.Game)
            {
                asymmetricProjectionMatrix = Matrix4x4.identity;
                viewportSubsection = Rect.zero;
                overscannedRect = Rect.zero;
                return false;
            }

            overscannedRect = m_ClusterRenderer.CalculateOverscannedRect(Screen.width, Screen.height);
            viewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection();
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
            {
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            }

            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            return true;
        }

        void AllocateSourceIfNeeded()
        {
            GraphicsUtil.AllocateIfNeeded(ref m_SourceRt, "Source", (int)m_OverscannedRect.width, (int)m_OverscannedRect.height, k_DefaultFormat);
        }
    }
}
