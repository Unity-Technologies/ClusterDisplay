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
        private bool m_HasClearedMirrorView = true;
        private Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.XRStitcher;
        public XRStitcherLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
            m_HasClearedMirrorView = true;
        }
        
        public override void Dispose()
        {
            m_QueuedStitcherParameters.Clear();
            ReleaseTargets();
        }

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.CameraContext != null)
                m_ClusterRenderer.CameraController.CameraContext.enabled = true;
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            Assert.IsFalse(m_QueuedStitcherParameters.Count == 0);
            var parms = m_QueuedStitcherParameters.Dequeue();
    
            var tileViewport = new Rect(
                viewport.x + viewport.width * parms.percentageViewportSubsection.x,
                viewport.y + viewport.height * parms.percentageViewportSubsection.y,
                viewport.width * parms.percentageViewportSubsection.width,
                viewport.height * parms.percentageViewportSubsection.height);

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
            if (!ValidGridSize(out var numTiles))
                return false;

            var camera = layout.camera;
            if (camera == null || camera.cameraType != CameraType.Game || !camera.TryGetCullingParameters(false, out var cullingParams))
                return false;

            // Whenever we build a new layout we expect previously submitted mirror params to have been consumed.
            Assert.IsTrue(m_QueuedStitcherParameters.Count == 0);
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
                
                CalculcateAndQueueStitcherParameters(i, m_OverscannedRect, percentageViewportSubsection);

                XRPass pass = layout.CreatePass(passInfo);
                layout.AddViewToPass(viewInfo, pass);
            }

            return true;
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            camera.targetTexture = null;
        }

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {}
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
#endif
}
