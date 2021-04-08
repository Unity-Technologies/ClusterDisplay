using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardStitcherLayoutBuilder : StitcherLayoutBuilder, ILayoutBuilder
    {
        public StandardStitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardStitcher;

        private RTHandle m_PresentTarget;
        private Rect m_OverscannedRect;

        private void PollPresentRT ()
        {
            bool resized = m_PresentTarget != null && (m_PresentTarget.rt.width != Screen.width || m_PresentTarget.rt.height != Screen.height);
            if (m_PresentTarget == null || resized)
            {
                if (m_PresentTarget != null)
                    RTHandles.Release(m_PresentTarget);

                m_PresentTarget = RTHandles.Alloc(
                    width: Screen.width, 
                    height: Screen.height,
                    slices: 1,
                    useDynamicScale: true,
                    autoGenerateMips: false,
                    name: "Present Target");
            }
        }

        public override void LateUpdate ()
        {
            var camera = m_ClusterRenderer.CameraController.CameraContext;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;

            Assert.IsTrue(m_QueuedStitcherParameters.Count == 0);

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return;

            PollPresentRT();
            PollRTs();

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);

            for (var i = 0; i < numTiles; i++)
            {
                CalculateStitcherLayout(
                    camera, 
                    i, 
                    ref cullingParams, 
                    out var percentageViewportSubsection, 
                    out var _, 
                    out var projectionMatrix);

                PollRT(i, m_OverscannedRect);
                CalculcateAndQueueStitcherParameters(i, m_OverscannedRect, percentageViewportSubsection);

                if (m_Targets[i] != null)
                    camera.targetTexture = m_Targets[i];

                camera.projectionMatrix = projectionMatrix;
                camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

                UploadClusterDisplayParams(projectionMatrix);
                camera.Render();
            }
        }

        public override void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
        public override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {}

        public override void OnEndCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            if (!ValidGridSize(out var numTiles))
                return;

            // Once the queue reaches the number of tiles, then we perform the blit copy.
            if (m_QueuedStitcherParameters.Count < numTiles)
                return;

            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentTarget;

            var cmd = CommandBufferPool.Get("BlitFinal");
            cmd.SetRenderTarget(m_PresentTarget);
            cmd.ClearRenderTarget(true, true, Color.black);

            for (var i = 0; i != numTiles; ++i)
            {
                if (m_Targets[i] == null)
                    continue;

                Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_ClusterRenderer.Context.GridSize, i);
                var stitcherParameters = m_QueuedStitcherParameters.Dequeue();

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);
                HDUtils.BlitQuad(cmd, stitcherParameters.target, stitcherParameters.scaleBiasTex, stitcherParameters.scaleBiasRT, 0, true);
            }

            context.ExecuteCommandBuffer(cmd);
        }

        public override void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) {}
    }
}
