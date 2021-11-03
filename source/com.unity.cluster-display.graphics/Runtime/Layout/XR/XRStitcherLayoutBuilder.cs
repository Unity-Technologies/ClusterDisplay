﻿#if CLUSTER_DISPLAY_HDRP && CLUSTER_DISPLAY_XR
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    class XRStitcherLayoutBuilder : StitcherLayoutBuilder, IXRLayoutBuilder
    {
        bool m_HasClearedMirrorView = true;
        Rect m_OverscannedRect;
        RTHandle[] m_SourceRts;

        public override ClusterRenderer.LayoutMode layoutMode => ClusterRenderer.LayoutMode.XRStitcher;

        public XRStitcherLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer)
        {
            m_HasClearedMirrorView = true;
        }

        public override void Dispose()
        {
            m_QueuedStitcherParameters.Clear();
            GraphicsUtil.DeallocateIfNeeded(ref m_SourceRts);
        }

        public override void LateUpdate()
        {
            if (k_ClusterRenderer.cameraController.TryGetContextCamera(out var contextCamera))
                contextCamera.enabled = true;
        }

        public void BuildMirrorView(XRPass pass, CommandBuffer cmd, RenderTexture rt, Rect viewport)
        {
            Assert.IsFalse(m_QueuedStitcherParameters.Count == 0);
            var parms = m_QueuedStitcherParameters.Dequeue();

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, k_ClusterRenderer.context.overscanInPixels);
            Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(k_ClusterRenderer.context.gridSize, parms.tileIndex);

            croppedViewport.x *= croppedSize.x;
            croppedViewport.y *= croppedSize.y;
            croppedViewport.width *= croppedSize.x;
            croppedViewport.height *= croppedSize.y;

            cmd.SetRenderTarget(rt);
            cmd.SetViewport(croppedViewport);

            if (!m_HasClearedMirrorView)
            {
                m_HasClearedMirrorView = true;
                cmd.ClearRenderTarget(true, true, k_ClusterRenderer.context.debug ? k_ClusterRenderer.context.bezelColor : Color.black);
            }

            var sourceRT = parms.sourceRT as RTHandle;
            if (sourceRT == null)
            {
                Debug.LogError($"Invalid {nameof(RTHandle)}");
                return;
            }

            HDUtils.BlitQuad(cmd, sourceRT, parms.scaleBiasTex, parms.scaleBiasRT, 0, true);
        }

        public bool BuildLayout(XRLayout layout)
        {
            if (!ValidGridSize(out var numTiles))
                return false;

            var camera = layout.camera;
            if (!k_ClusterRenderer.cameraController.CameraIsInContext(camera))
                return false;

            if (!camera.enabled)
                camera.enabled = true;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return false;

            // Whenever we build a new layout we expect previously submitted mirror params to have been consumed.
            Assert.IsTrue(m_QueuedStitcherParameters.Count == 0);
            m_HasClearedMirrorView = false;

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            var cachedProjectionMatrix = camera.projectionMatrix;

            GraphicsUtil.AllocateIfNeeded(ref m_SourceRts, numTiles, "Source", (int)m_OverscannedRect.width, (int)m_OverscannedRect.height);
            
            for (var tileIndex = 0; tileIndex != numTiles; ++tileIndex)
            {
                CalculateStitcherLayout(
                    camera,
                    cachedProjectionMatrix,
                    tileIndex,
                    out var percentageViewportSubsection,
                    out var viewportSubsection,
                    out var asymmetricProjectionMatrix);

                cullingParams.stereoProjectionMatrix = asymmetricProjectionMatrix;
                cullingParams.stereoViewMatrix = camera.worldToCameraMatrix;
                cullingParams.cullingMatrix = camera.worldToCameraMatrix * asymmetricProjectionMatrix;

                CalculcateAndQueueStitcherParameters(tileIndex, m_SourceRts[tileIndex], m_OverscannedRect, percentageViewportSubsection);

                var clusterDisplayParams = GraphicsUtil.GetClusterDisplayParams(
                    viewportSubsection,
                    k_ClusterRenderer.context.globalScreenSize,
                    k_ClusterRenderer.context.gridSize);

                var passInfo = new XRPassCreateInfo
                {
                    multipassId = tileIndex,
                    cullingPassId = tileIndex,
                    cullingParameters = cullingParams,
                    renderTarget = m_SourceRts[tileIndex],
                    customMirrorView = BuildMirrorView
                };

                var viewInfo = new XRViewCreateInfo
                {
                    viewMatrix = camera.worldToCameraMatrix,
                    projMatrix = asymmetricProjectionMatrix,
                    viewport = m_OverscannedRect,
                    clusterDisplayParams = clusterDisplayParams,
                    textureArraySlice = -1
                };

                XRPass pass = layout.CreatePass(passInfo);
                layout.AddViewToPass(viewInfo, pass);
            }

            return true;
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }

        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (!k_ClusterRenderer.cameraController.TryGetContextCamera(out var contextCamera) || camera != contextCamera)
                return;

            camera.targetTexture = null;
        }

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }
        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
    }
}
#endif
