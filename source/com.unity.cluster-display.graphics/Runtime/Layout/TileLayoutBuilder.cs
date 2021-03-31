using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileLayoutBuilder : LayoutBuilder
    {
        protected static readonly Vector4 k_ScaleBiasRT = new Vector4(1, 1, 0, 0);

        protected RTHandle m_OverscannedTarget;
        protected Rect m_OverscannedRect;
        protected int m_OverscanInPixels;

        // Allow overscanned pixels visualization for debugging purposes.
        protected Vector2 m_DebugScaleBiasTexOffset;

        protected TileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        protected bool SetupLayout (Camera camera, out ScriptableCullingParameters cullingParameters, out Matrix4x4 projectionMatrix, out Rect viewportSubsection)
        {
            cullingParameters = default(ScriptableCullingParameters);
            projectionMatrix = Matrix4x4.identity;
            viewportSubsection = Rect.zero;

            if (camera.cameraType != CameraType.Game || !camera.TryGetCullingParameters(false, out cullingParameters))
                return false;

            m_DebugScaleBiasTexOffset = m_ClusterRenderer.Context.DebugScaleBiasTexOffset;
            m_OverscanInPixels = m_ClusterRenderer.Context.OverscanInPixels;
            m_OverscannedRect = new Rect(0, 0, 
                Screen.width + 2 * m_ClusterRenderer.Context.OverscanInPixels, 
                Screen.height + 2 * m_ClusterRenderer.Context.OverscanInPixels);

            viewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection();
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            projectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            
            if (m_OverscannedTarget == null)
                m_OverscannedTarget = RTHandles.Alloc(Vector2.one, 1, dimension: TextureXR.dimension, useDynamicScale: true, autoGenerateMips: false, name: "Overscanned Target");

            return true;
        }
    }
}
