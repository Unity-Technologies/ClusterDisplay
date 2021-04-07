using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        private Vector3[] m_TargetScaleBias = null;
        private Rect m_OverscannedRect;

        public override void LateUpdate ()
        {
            var camera = m_ClusterRenderer.CameraController.CameraContext;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;

            if (!ValidGridSize(out var numTiles))
                return;

            if (!camera.TryGetCullingParameters(false, out var cullingParams))
                return;

            PollRTs();
            if (m_TargetScaleBias == null || m_TargetScaleBias.Length != numTiles)
                m_TargetScaleBias = new Vector3[numTiles];

            m_OverscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);

            for (var i = 0; i != numTiles; ++i)
            {
                CalculateStitcherLayout(
                    camera, 
                    i, 
                    ref cullingParams, 
                    out var _, 
                    out var _, 
                    out var projectionMatrix);

                PollRT(i, m_OverscannedRect);

                if (m_Targets[i] != null)
                    camera.targetTexture = m_Targets[i];

                UploadClusterDisplayParams(projectionMatrix);

                m_TargetScaleBias[i] = CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
                camera.projectionMatrix = projectionMatrix;
                camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }
        }

        public override void OnBeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            if (!ValidGridSize(out var numTiles))
                return;

            {
                Vector2 croppedSize = new Vector2(Screen.width, Screen.height);
                bool resized = m_PresentTarget != null && (m_PresentTarget.rt.width != (int)croppedSize.x || m_PresentTarget.rt.height != (int)croppedSize.y);
                if (m_PresentTarget == null || resized)
                {
                    if (m_PresentTarget != null)
                        RTHandles.Release(m_PresentTarget);

                    m_PresentTarget = RTHandles.Alloc(
                        width: (int)croppedSize.x, 
                        height: (int)croppedSize.y,
                        slices: 1,
                        useDynamicScale: true,
                        autoGenerateMips: false,
                        name: "Present Target");
                }
            }

            camera.enabled = false;
        }

        public override void OnEndRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            if (!ValidGridSize(out var numTiles))
                return;

            CalculateScaleBias(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels, m_ClusterRenderer.Context.DebugScaleBiasTexOffset);
            var croppedSize = CalculateCroppedSize(m_OverscannedRect, m_ClusterRenderer.Context.OverscanInPixels);
            var cmd = CommandBufferPool.Get("BlitFinal");

            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentTarget;
            cmd.SetRenderTarget(m_PresentTarget);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            for (var i = 0; i != numTiles; ++i)
            {
                if (m_Targets[i] == null)
                    continue;

                Rect croppedViewport = GraphicsUtil.TileIndexToViewportSection(m_ClusterRenderer.Context.GridSize, i);

                croppedViewport.x *= croppedSize.x;
                croppedViewport.y *= croppedSize.y;
                croppedViewport.width *= croppedSize.x;
                croppedViewport.height *= croppedSize.y;

                cmd.SetViewport(croppedViewport);
                HDUtils.BlitQuad(cmd, m_Targets[i], m_TargetScaleBias[i], k_ScaleBiasRT, 0, true);
            }

            context.ExecuteCommandBuffer(cmd);
        }
    }
}
