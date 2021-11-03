using UnityEngine;
using UnityEngine.Rendering;
using GraphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Disables the camera and calls Camera.Render() for a single tile.
    /// </summary>
    class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        const GraphicsFormat k_DefaultFormat = GraphicsFormat.R8G8B8A8_SRGB;
        
        Rect m_OverscannedRect;
        RenderTexture m_SourceRt;
        RenderTexture m_PresentRt;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.StandardTile;

        protected StandardTileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }

        public override void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRt);
            GraphicsUtil.DeallocateIfNeeded(ref m_PresentRt);
        }

        public override void LateUpdate()
        {
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var camera))
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

            AllocateSourceIfNeeded();
            camera.targetTexture = m_SourceRt;
            camera.projectionMatrix = asymmetricProjectionMatrix;
            camera.cullingMatrix = asymmetricProjectionMatrix * camera.worldToCameraMatrix;

            camera.Render();

            camera.ResetProjectionMatrix();
            camera.ResetCullingMatrix();
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return;
            
            var cmd = CommandBufferPool.Get("BlitToClusteredPresent");

            GraphicsUtil.AllocateIfNeeded(ref m_PresentRt, "Present", Screen.width, Screen.height, k_DefaultFormat);
            cmd.SetRenderTarget(m_PresentRt);
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            var scaleBias = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            Blit(cmd, m_SourceRt, scaleBias, k_ScaleBiasRT);

            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            k_ClusterRenderer.cameraController.presenter.presentRT = m_PresentRt;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        
        void AllocateSourceIfNeeded() => GraphicsUtil.AllocateIfNeeded(ref m_SourceRt, "Source", (int)m_OverscannedRect.width, (int)m_OverscannedRect.height, k_DefaultFormat);
    }
}
