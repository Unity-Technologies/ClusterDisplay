using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace Unity.ClusterDisplay.Graphics
{
    public class StandardTileLayoutBuilder : TileLayoutBuilder, ILayoutBuilder
    {
        public StandardTileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        public override void Dispose() {}

        private RTHandle m_PresentTarget;
        public override void OnBeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ClusterRenderer.CameraController.CameraContext)
                return;

            if (!SetupLayout(camera, out var cullingParams, out var projMatrix, out var viewportSubsection))
                return;

            m_ClusterRenderer.CameraController.CameraContext.enabled = false;
            camera.projectionMatrix = projMatrix;
            camera.cullingMatrix = projMatrix * camera.worldToCameraMatrix;
            m_ClusterRenderer.CameraController.CameraContextRenderTexture = m_OverscannedTarget;

            Shader.SetGlobalMatrix("_ClusterDisplayParams", camera.projectionMatrix);

            CalculateParameters(out var croppedSize, out var overscannedSize, out var scaleBias);
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

        public override ClusterRenderer.LayoutMode LayoutMode => ClusterRenderer.LayoutMode.StandardTile;

        public override void LateUpdate()
        {
            if (m_ClusterRenderer.CameraController.CameraContext == null)
                return;

            m_ClusterRenderer.CameraController.CameraContext.Render();
        }

        public override void OnEndRender(ScriptableRenderContext context, Camera camera)
        {
            CalculateParameters(out var croppedSize, out var overscannedSize, out var scaleBias);

            var cmd = CommandBufferPool.Get("BlitFinal");
            m_ClusterRenderer.CameraController.Presenter.PresentRT = m_PresentTarget;

            cmd.SetRenderTarget(m_PresentTarget);
            cmd.ClearRenderTarget(true, true, Color.black);

            HDUtils.BlitQuad(cmd, m_OverscannedTarget, scaleBias, new Vector4(1, 1, 0, 0), 0, true);
            context.ExecuteCommandBuffer(cmd);
        }
    }
}
