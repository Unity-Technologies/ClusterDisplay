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

        private Vector3[] targetScaleBias;

        public override void LateUpdate ()
        {
            var camera = m_ClusterRenderer.CameraController.CameraContext;
            if (camera == null)
                return;

            if (camera.enabled)
                camera.enabled = false;

            var numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y;
            if (numTiles <= 0)
                return;

            if (m_Targets == null || m_Targets.Length != numTiles)
            {
                m_Targets = new RTHandle[numTiles];
                targetScaleBias = new Vector3[numTiles];
            }

            for (var i = 0; i != numTiles; ++i)
            {
                SetupLayout(camera, i, out var overscannedSize, out var viewportSubsection);

                bool resized = m_Targets[i] != null && (m_Targets[i].rt.width != (int)overscannedSize.x || m_Targets[i].rt.height != (int)overscannedSize.y);
                if (m_Targets[i] == null || resized)
                {
                    if (m_Targets[i] != null)
                        RTHandles.Release(m_Targets[i]);

                    targetScaleBias[i] = k_ScaleBiasRT;
                    m_Targets[i] = RTHandles.Alloc(
                        width: (int)overscannedSize.width,
                        height: (int)overscannedSize.height,
                        slices: 1,
                        dimension: TextureXR.dimension,
                        useDynamicScale: false,
                        autoGenerateMips: false,
                        enableRandomWrite: true,
                        name: $"Tile Target {i}");
                }
            }

            for (var i = 0; i != numTiles; ++i)
            {
                var target = m_Targets[i];
                if (target != null)
                    camera.targetTexture = target;

                SetupLayout(camera, i, out var overscannedRect, out var viewportSubsection);

                var overscanInPixels = m_ClusterRenderer.Context.OverscanInPixels;
                var croppedSize = new Vector2(overscannedRect.width - 2 * overscanInPixels, overscannedRect.height - 2 * overscanInPixels);
                var overscannedSize = new Vector2(overscannedRect.width, overscannedRect.height);
                var scaleBias = new Vector4(
                    croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                    overscanInPixels / overscannedSize.x, overscanInPixels / overscannedSize.y); // offset
                scaleBias.z += m_DebugScaleBiasTexOffset.x;
                scaleBias.w += m_DebugScaleBiasTexOffset.y;

                targetScaleBias[i] = scaleBias;

                var projectionMatrix = SetupMatrices(camera, viewportSubsection);
                camera.projectionMatrix = projectionMatrix;
                camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;

                camera.Render();
            }
        }

        public override void OnBeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            var numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y;
            if (numTiles <= 0)
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

            var numTiles = m_ClusterRenderer.Context.GridSize.x * m_ClusterRenderer.Context.GridSize.y;
            if (numTiles <= 0)
                return;

            CalculateParameters(out var croppedSize, out var overscannedSize, out var scaleBias);
            var cmd = CommandBufferPool.Get("BlitFinal");

            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentTarget;
            cmd.SetRenderTarget(m_PresentTarget);
            // var viewport = new Rect(0, 0, croppedSize.x, croppedSize.y);
            // cmd.SetViewport(viewport);
            cmd.ClearRenderTarget(true, true, Color.yellow);

            for (var i = 0; i != numTiles; ++i)
            {
                Rect viewport = GraphicsUtil.TileIndexToViewportSection(m_ClusterRenderer.Context.GridSize, i);

                viewport.x *= Screen.width;
                viewport.y *= Screen.height;
                viewport.width *= Screen.width;
                viewport.height *= Screen.height;

                SetupLayout(camera, i, out var overscannedRect, out var viewportSubsection);
                cmd.SetViewport(viewport);
                HDUtils.BlitQuad(cmd, m_Targets[i], targetScaleBias[i], new Vector4(1, 1, 0, 0), 0, true);
            }

            context.ExecuteCommandBuffer(cmd);
        }
    }
}
