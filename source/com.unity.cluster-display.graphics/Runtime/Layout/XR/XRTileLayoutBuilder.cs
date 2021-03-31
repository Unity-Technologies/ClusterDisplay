using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
#if CLUSTER_DISPLAY_XR
    class XRTileLayoutBuilder : TileLayoutBuilder, IXRLayoutBuilder
    {
        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.XRTile;

        public XRTileLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        public override void Dispose()
        {
            RTHandles.Release(m_OverscannedTarget);
            m_OverscannedTarget = null;
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true, true, Color.yellow);
            
            // Blit so that overscanned pixels are cropped out.
            var croppedSize = new Vector2(m_OverscannedRect.width - 2 * m_OverscanInPixels, m_OverscannedRect.height - 2 * m_OverscanInPixels);
            var overscannedSize = new Vector2(m_OverscannedTarget.rt.width, m_OverscannedTarget.rt.height);
            var scaleBiasTex = new Vector4(
                croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                m_OverscanInPixels / overscannedSize.x, m_OverscanInPixels / overscannedSize.y); // offset
            // Debug: allow visualization of overscanned pixels.
            scaleBiasTex.z += m_DebugScaleBiasTexOffset.x;
            scaleBiasTex.w += m_DebugScaleBiasTexOffset.y;
            
            HDUtils.BlitQuad(cmd, m_OverscannedTarget, scaleBiasTex, k_ScaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            var camera = layout.camera;

            if (!SetupLayout(camera, out var cullingParams, out var projMatrix, out var viewportSubsection))
                return false;

            var passInfo = new XRPassCreateInfo
            {
                multipassId = 0,
                cullingPassId = 0,
                cullingParameters = cullingParams,
                renderTarget = m_OverscannedTarget,
                customMirrorView = BuildMirrorView
            };
            
            var viewInfo = new XRViewCreateInfo
            {
                viewMatrix = camera.worldToCameraMatrix,
                projMatrix = projMatrix,
                viewport = m_OverscannedRect,
                clusterDisplayParams = GraphicsUtil.GetHdrpClusterDisplayParams(
                    viewportSubsection, m_ClusterRenderer.Context.GlobalScreenSize, m_ClusterRenderer.Context.GridSize),
                textureArraySlice = -1
            };

            passInfo.multipassId = 0;
            XRPass pass = layout.CreatePass(passInfo);
            layout.AddViewToPass(viewInfo, pass);

            onReceiveLayout(camera);

            return true;
        }
    }
#endif
}
