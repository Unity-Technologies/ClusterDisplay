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

        public XRTileLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
        }

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

            CalculateParameters(out var croppedSize, out var overscannedSize, out var scaleBiasTex);
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

            onReceiveLayout(camera);

            return true;
        }

        public override void OnBeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;
            camera.targetTexture = null;
        }

        public override void OnEndRender(ScriptableRenderContext context, Camera camera)
        {
        }

        public override void LateUpdate()
        {
            m_ClusterRenderer.CameraController.CameraContext.enabled = true;
        }
    }
#endif
}
