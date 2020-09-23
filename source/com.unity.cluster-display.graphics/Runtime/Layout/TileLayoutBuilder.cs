﻿using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class TileLayoutBuilder : ILayoutBuilder
    {
        static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);

        RTHandle m_OverscannedTarget;
        Rect m_OverscannedRect;
        int m_OverscanInPixels;

        // Allow overscanned pixels visualization for debugging purposes.
        Vector2 m_DebugScaleBiasTexOffset;
        
        public void Initialize() { }

        public void Dispose()
        {
            RTHandles.Release(m_OverscannedTarget);
            m_OverscannedTarget = null;
        }

        public bool BuildLayout(XRLayout layout, ClusterRenderContext context, ScriptableCullingParameters cullingParams)
        {
            m_DebugScaleBiasTexOffset = context.DebugScaleBiasTexOffset;
            m_OverscanInPixels = context.OverscanInPixels;
            m_OverscannedRect = new Rect(0, 0, 
                Screen.width + 2 * context.OverscanInPixels, 
                Screen.height + 2 * context.OverscanInPixels);

            var viewportSubsection = context.GetViewportSubsection();
            if (context.PhysicalScreenSize != Vector2Int.zero && context.Bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, context.PhysicalScreenSize, context.Bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, context.OverscanInPixels);
            
            var camera = layout.camera;
            var projMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(
                camera.projectionMatrix, viewportSubsection);
            cullingParams.stereoProjectionMatrix = projMatrix;
            cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
            
            if (m_OverscannedTarget == null)
                m_OverscannedTarget = RTHandles.Alloc(Vector2.one, 1, dimension: TextureXR.dimension, useDynamicScale: true, autoGenerateMips: false, name: "Overscanned Target");

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
                    viewportSubsection, context.GlobalScreenSize, context.GridSize),
                textureArraySlice = -1
            };

            passInfo.multipassId = 0;
            XRPass pass = layout.CreatePass(passInfo);
            layout.AddViewToPass(viewInfo, pass);
            return true;
        }
        
        void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
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
    }
}