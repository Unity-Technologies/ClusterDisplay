using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
#if CLUSTER_DISPLAY_XR
    class XRStitcherLayoutBuilder : StitcherLayoutBuilder, IXRLayoutBuilder
    {
        struct MirrorParams
        {
            public RTHandle target;
            public Vector4 scaleBiasTex;
            public Vector4 scaleBiasRT;
            public Rect viewportSubsection;
        }

        // Assumes one mirror view callback execution per pass.
        private Queue<MirrorParams> m_MirrorParams = new Queue<MirrorParams>();
        private bool m_HasClearedMirrorView = true;
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.XRStitcher;
        public XRStitcherLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
            m_HasClearedMirrorView = true;
        }
        
        public override void Dispose()
        {
            m_MirrorParams.Clear();
            ReleaseTargets();
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            Assert.IsFalse(m_MirrorParams.Count == 0);

            var parms = m_MirrorParams.Dequeue();
    
            var tileViewport = new Rect(
                viewport.x + viewport.width * parms.viewportSubsection.x,
                viewport.y + viewport.height * parms.viewportSubsection.y,
                viewport.width * parms.viewportSubsection.width,
                viewport.height * parms.viewportSubsection.height);

            cmd.SetRenderTarget(rt);
            
            if (!m_HasClearedMirrorView)
            {
                m_HasClearedMirrorView = true;
                cmd.ClearRenderTarget(true, true, Color.black);
            }

            cmd.SetViewport(tileViewport);
            
            HDUtils.BlitQuad(cmd, parms.target, parms.scaleBiasTex, parms.scaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            var numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y;
            if (numTiles <= 0)
                return false;

            var camera = layout.camera;
            if (camera == null || camera.cameraType != CameraType.Game || !camera.TryGetCullingParameters(false, out var cullingParams))
                return false;

            // Whenever we build a new layout we expect previously submitted mirror params to have been consumed.
            Assert.IsTrue(m_MirrorParams.Count == 0);
            // Assert.IsTrue(m_HasClearedMirrorView);
            m_HasClearedMirrorView = false;

            PollRTs();
            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            
            for (var i = 0; i != numTiles; ++i)
            {
                PollRT(i, m_OverscannedRect);
                CalculateStitcherLayout(
                    camera, 
                    i, 
                    ref cullingParams, 
                    out var percentageViewportSubsection, 
                    out var viewportSubsection, 
                    out var projectionMatrix);
               
                var passInfo = new XRPassCreateInfo
                {
                    multipassId = i,
                    cullingPassId = 0,
                    cullingParameters = cullingParams,
                    renderTarget = m_Targets[i],
                    customMirrorView = BuildMirrorView
                };
                
                var viewInfo = new XRViewCreateInfo
                {
                    viewMatrix = camera.worldToCameraMatrix,
                    projMatrix = projectionMatrix,
                    viewport = m_OverscannedRect,
                    clusterDisplayParams = GraphicsUtil.GetHdrpClusterDisplayParams(
                        viewportSubsection, 
                        m_ClusterRenderer.Context.GlobalScreenSize, 
                        m_ClusterRenderer.Context.GridSize),
                    textureArraySlice = -1
                };
                
                XRPass pass = layout.CreatePass(passInfo);
                layout.AddViewToPass(viewInfo, pass);

                var scaleBiasTex = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
                var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
                
                var scaleBiasRT = new Vector4(
                    1 - (m_ClusterRenderer.Context.Bezel.x * 2) / croppedSize.x, 1 - (m_ClusterRenderer.Context.Bezel.y * 2) / croppedSize.y, // scale
                    m_ClusterRenderer.Context.Bezel.x / croppedSize.x, m_ClusterRenderer.Context.Bezel.y / croppedSize.y); // offset
                
                m_MirrorParams.Enqueue(new MirrorParams
                {
                    scaleBiasTex = scaleBiasTex,
                    scaleBiasRT = scaleBiasRT,
                    viewportSubsection = percentageViewportSubsection,
                    target = m_Targets[i]
                });
            }

            return true;
        }

        public override void OnBeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            camera.targetTexture = null;
        }

        public override void OnEndRender(ScriptableRenderContext context, Camera camera) {}

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.CameraContext != null)
                m_ClusterRenderer.CameraController.CameraContext.enabled = true;
        }
    }
#endif
}
