using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class StitcherLayoutBuilder : LayoutBuilder
    {
        protected RTHandle[] m_Targets;
        protected StitcherLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}
        protected void ReleaseTargets()
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

        protected Matrix4x4 SetupMatrices (Camera camera, Rect viewportSubsection) => GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
        protected bool SetupLayout(
            Camera camera,
            int i,
            out Rect overscannedRect,
            out Rect viewportSubsection)
        {
            if (!camera.TryGetCullingParameters(false, out var cullingParams))
            {
                overscannedRect = Rect.zero;
                viewportSubsection = Rect.zero;
                return false;
            }

            overscannedRect = new Rect(0, 0, 
                Screen.width + 2 * m_ClusterRenderer.Context.OverscanInPixels, 
                Screen.height + 2 * m_ClusterRenderer.Context.OverscanInPixels);

            var originalViewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection(i);
            viewportSubsection = originalViewportSubsection;
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            var croppedSize = new Vector2(m_OverscannedRect.width - 2 * m_ClusterRenderer.Context.OverscanInPixels, m_OverscannedRect.height - 2 * m_ClusterRenderer.Context.OverscanInPixels);
            var targetSize = new Vector2(m_OverscannedRect.width, m_OverscannedRect.height);
            return true;
        }
    }
}
