#if CLUSTER_DISPLAY_HDRP && CLUSTER_DISPLAY_XR
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class XRTileLayoutBuilder : TileLayoutBuilder, IXRLayoutBuilder
    {
        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.XRTile;

        Rect m_OverscannedRect;
        RTHandle m_SourceRTHandle;

        public XRTileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }

        public override void Dispose()
        {
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRTHandle);
        }

        public override void LateUpdate()
        {
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var contextCamera))
                return;

            contextCamera.enabled = true;
            contextCamera.targetTexture = null;
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);

            HDUtils.BlitQuad(cmd, m_SourceRTHandle, scaleBiasTex, k_ScaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            var camera = layout.camera;
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return false;

            if (!camera.enabled)
                camera.enabled = true;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return false;

            if (!SetupTiledLayout(
                camera,
                out var asymmetricProjectionMatrix,
                out var viewportSubsection,
                out m_OverscannedRect))
                return false;

            cullingParams.stereoProjectionMatrix = asymmetricProjectionMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;

            GraphicsUtil.AllocateIfNeeded(ref m_SourceRTHandle, "Source", (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            var passInfo = new XRPassCreateInfo
            {
                multipassId = 0,
                cullingPassId = 0,
                cullingParameters = cullingParams,
                renderTarget = m_SourceRTHandle,
                customMirrorView = BuildMirrorView
            };

            var clusterDisplayParams = GraphicsUtil.GetClusterDisplayParams(
                viewportSubsection,
                k_ClusterRenderer.context.globalScreenSize,
                k_ClusterRenderer.context.gridSize);

            var viewInfo = new XRViewCreateInfo
            {
                viewMatrix = camera.worldToCameraMatrix,
                projMatrix = asymmetricProjectionMatrix,
                viewport = m_OverscannedRect,
                clusterDisplayParams = clusterDisplayParams,
                textureArraySlice = -1
            };

            var pass = layout.CreatePass(passInfo);
            layout.AddViewToPass(viewInfo, pass);

            return true;
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
    }
}
#endif
