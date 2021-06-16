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

        private XRTileRTManager m_RTManager = new XRTileRTManager();
        private Rect m_OverscannedRect;

        public XRTileLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose()
        {
            m_RTManager.Release();
        }

        public override void LateUpdate()
        {
            if (k_ClusterRenderer.cameraController.contextCamera != null)
            {
                k_ClusterRenderer.cameraController.contextCamera.enabled = true;
                k_ClusterRenderer.cameraController.contextCamera.targetTexture = null;
            }
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);

            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels, k_ClusterRenderer.context.debugScaleBiasTexOffset);
            var target = m_RTManager.GetBlitRTHandle((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            HDUtils.BlitQuad(cmd, target, scaleBiasTex, k_ScaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            var camera = layout.camera;
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return false;

            if (!SetupTiledLayout(
                camera, 
                out var cullingParams, 
                out var projMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return false;

            cullingParams.stereoProjectionMatrix = projMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
            var target = m_RTManager.GetBlitRTHandle((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            var passInfo = new XRPassCreateInfo
            {
                multipassId = 0,
                cullingPassId = 0,
                cullingParameters = cullingParams,
                renderTarget = target,
                customMirrorView = BuildMirrorView
            };

            var clusterDisplayParams = GraphicsUtil.GetClusterDisplayParams(
                viewportSubsection, 
                k_ClusterRenderer.context.globalScreenSize, 
                k_ClusterRenderer.context.gridSize);

            var viewInfo = new XRViewCreateInfo
            {
                viewMatrix = camera.worldToCameraMatrix,
                projMatrix = projMatrix,
                viewport = m_OverscannedRect,
                clusterDisplayParams = clusterDisplayParams,
                textureArraySlice = -1
            };

            passInfo.multipassId = 0;
            XRPass pass = layout.CreatePass(passInfo);
            layout.AddViewToPass(viewInfo, pass);

            return true;
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
#endif
