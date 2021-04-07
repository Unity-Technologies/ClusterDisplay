using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileLayoutBuilder : LayoutBuilder
    {
        protected RTHandle m_OverscannedTarget;

        protected TileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        protected bool SetupLayout (Camera camera, out ScriptableCullingParameters cullingParameters, out Matrix4x4 projectionMatrix, out Rect viewportSubsection)
        {
            if (camera.cameraType != CameraType.Game || !camera.TryGetCullingParameters(false, out cullingParameters))
            {
                cullingParameters = default(ScriptableCullingParameters);
                projectionMatrix = Matrix4x4.identity;
                viewportSubsection = Rect.zero;
                return false;
            }

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
            
            bool resized = m_OverscannedTarget != null && (m_OverscannedTarget.rt.width != (int)m_OverscannedRect.width || m_OverscannedTarget.rt.height != (int)m_OverscannedRect.height);
            if (m_OverscannedTarget == null || resized)
            {
                if (m_OverscannedTarget != null)
                {
                    if (camera.targetTexture != null && camera.targetTexture == m_OverscannedTarget)
                        camera.targetTexture = null;
                    RTHandles.Release(m_OverscannedTarget);
                }

                m_OverscannedTarget = RTHandles.Alloc(
                    width: (int)m_OverscannedRect.width, 
                    height: (int)m_OverscannedRect.height, 
                    slices: 1, 
                    dimension: TextureXR.dimension, 
                    useDynamicScale: true, 
                    autoGenerateMips: false, 
                    name: "Overscanned Target");
            }

            cullingParameters.stereoProjectionMatrix = projectionMatrix;
            cullingParameters.stereoViewMatrix = camera.worldToCameraMatrix;

            return true;
        }
    }
}
