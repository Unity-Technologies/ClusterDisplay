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

        static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);

        // Assumes one mirror view callback execution per pass.
        Queue<MirrorParams> m_MirrorParams = new Queue<MirrorParams>();
        RTHandle[] m_Targets;
        bool m_HasClearedMirrorView = true;
        Rect m_OverscannedRect;

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.XRStitcher;

        public XRStitcherLayoutBuilder (IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        
        public override void Dispose()
        {
            m_MirrorParams.Clear();
            ReleaseTargets();
        }

        void ReleaseTargets()
        {
            if (m_Targets != null)
            {
                for (var i = 0; i != m_Targets.Length; ++i)
                {
                    RTHandles.Release(m_Targets[i]);
                    m_Targets[i] = null;
                }
            }
            m_Targets = null;
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
            
            // Whenever we build a new layout we expect previously submitted mirror params to have been consumed.
            Assert.IsTrue(m_MirrorParams.Count == 0);
            Assert.IsTrue(m_HasClearedMirrorView);
            m_HasClearedMirrorView = false;
            
            // Generate/re-generate targets if needed.
            if (m_Targets == null || m_Targets.Length != numTiles)
            {
                ReleaseTargets();
                m_Targets = new RTHandle[numTiles];
                
                for (var i = 0; i != numTiles; ++i)
                {
                    m_Targets[i] = RTHandles.Alloc(
                        Vector2.one, 1, 
                        dimension: TextureXR.dimension, 
                        useDynamicScale: false, 
                        autoGenerateMips: false,                     
                        enableRandomWrite: true,
                        name: $"Tile Target {i}");
                }
            }
            
            m_OverscannedRect = new Rect(0, 0, 
                Screen.width + 2 * m_ClusterRenderer.Context.OverscanInPixels, 
                Screen.height + 2 * m_ClusterRenderer.Context.OverscanInPixels);

            var camera = layout.camera;
            if (!(camera != null && camera.cameraType == CameraType.Game && camera.TryGetCullingParameters(false, out var cullingParams)))
                return false;
            
            for (var i = 0; i != numTiles; ++i)
            {
                var originalViewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection(i);
                var viewportSubsection = originalViewportSubsection;
                if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
                    viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
                viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

                var projMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
                cullingParams.stereoProjectionMatrix = projMatrix;
                cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
               
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
                    projMatrix = projMatrix,
                    viewport = m_OverscannedRect,
                    clusterDisplayParams = GraphicsUtil.GetHdrpClusterDisplayParams(
                        viewportSubsection, m_ClusterRenderer.Context.GlobalScreenSize, m_ClusterRenderer.Context.GridSize),
                    textureArraySlice = -1
                };
                
                XRPass pass = layout.CreatePass(passInfo);
                layout.AddViewToPass(viewInfo, pass);

                // Blit so that overscanned pixels are cropped out.
                var croppedSize = new Vector2(m_OverscannedRect.width - 2 * m_ClusterRenderer.Context.OverscanInPixels, m_OverscannedRect.height - 2 * m_ClusterRenderer.Context.OverscanInPixels);
                var targetSize = new Vector2(m_Targets[i].rt.width, m_Targets[i].rt.height);
                var scaleBiasTex = new Vector4(
                    croppedSize.x / targetSize.x, croppedSize.y / targetSize.y, // scale
                    m_ClusterRenderer.Context.OverscanInPixels / targetSize.x, m_ClusterRenderer.Context.OverscanInPixels / targetSize.y); // offset
                
                // Account for bezel when compositing
                var scaleBiasRT = new Vector4(
                    1 - (m_ClusterRenderer.Context.Bezel.x * 2) / croppedSize.x, 1 - (m_ClusterRenderer.Context.Bezel.y * 2) / croppedSize.y, // scale
                    m_ClusterRenderer.Context.Bezel.x / croppedSize.x, m_ClusterRenderer.Context.Bezel.y / croppedSize.y); // offset
                
                m_MirrorParams.Enqueue(new MirrorParams
                {
                    scaleBiasTex = scaleBiasTex,
                    scaleBiasRT = scaleBiasRT,
                    viewportSubsection = originalViewportSubsection,
                    target = m_Targets[i]
                });
            }

            onReceiveLayout(camera);
            return true;
        }
    }
#endif
}
