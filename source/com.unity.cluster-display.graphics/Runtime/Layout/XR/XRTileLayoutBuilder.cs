#if CLUSTER_DISPLAY_HDRP && CLUSTER_DISPLAY_XR
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class XRTileLayoutBuilder : TileLayoutBuilder, IXRLayoutBuilder
    {
        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.XRTile;

        private XRTileRTManager m_RTManager = new XRTileRTManager();
        private Rect m_OverscannedRect;

        public XRTileLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose()
        {
            m_RTManager.Release();
        }

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.ContextCamera != null)
            {
                m_ClusterRenderer.CameraController.ContextCamera.enabled = true;
                m_ClusterRenderer.CameraController.ContextCamera.targetTexture = null;
            }
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
            var target = m_RTManager.BlitRTHandle((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            HDUtils.BlitQuad(cmd, target, scaleBiasTex, k_ScaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            var camera = layout.camera;

            if (!SetupTiledLayout(
                camera, 
                out var cullingParams, 
                out var projMatrix, 
                out var viewportSubsection,
                out m_OverscannedRect))
                return false;

            cullingParams.stereoProjectionMatrix = projMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
            var target = m_RTManager.BlitRTHandle((int)m_OverscannedRect.width, (int)m_OverscannedRect.height);

            var passInfo = new XRPassCreateInfo
            {
                multipassId = 0,
                cullingPassId = 0,
                cullingParameters = cullingParams,
                renderTarget = target,
                customMirrorView = BuildMirrorView
            };

            var clusterDisplayParams = GraphicsUtil.GetHdrpClusterDisplayParams(
                viewportSubsection, 
                m_ClusterRenderer.Context.GlobalScreenSize, 
                m_ClusterRenderer.Context.GridSize);

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
