using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileLayoutBuilder : LayoutBuilder
    {
        protected TileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
        }

        protected bool SetupTiledLayout (
            Camera camera, 
            ref RTHandle targetRT,
            out ScriptableCullingParameters cullingParameters, 
            out Matrix4x4 projectionMatrix,
            out Rect viewportSubsection,
            out Rect overscannedRect)
        {
            if (!ValidGridSize(out var numTiles) || 
                camera.cameraType != CameraType.Game || 
                !camera.TryGetCullingParameters(false, out cullingParameters))
            {
                cullingParameters = default(ScriptableCullingParameters);
                targetRT = null;
                projectionMatrix = Matrix4x4.identity;
                viewportSubsection = Rect.zero;
                overscannedRect = Rect.zero;
                return false;
            }

            overscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            viewportSubsection = m_ClusterRenderer.Context.GetViewportSubsection();
            if (m_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && m_ClusterRenderer.Context.Bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, m_ClusterRenderer.Context.PhysicalScreenSize, m_ClusterRenderer.Context.Bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, m_ClusterRenderer.Context.OverscanInPixels);

            projectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            
            bool resized = targetRT != null && (targetRT.rt.width != (int)overscannedRect.width || targetRT.rt.height != (int)overscannedRect.height);
            if (targetRT == null || resized)
            {
                if (targetRT != null)
                {
                    if (camera.targetTexture != null && camera.targetTexture == targetRT)
                        camera.targetTexture = null;
                    RTHandles.Release(targetRT);
                }

                targetRT = RTHandles.Alloc(
                    width: (int)overscannedRect.width, 
                    height: (int)overscannedRect.height, 
                    slices: 1, 
                    dimension: TextureXR.dimension, 
                    useDynamicScale: true, 
                    autoGenerateMips: false, 
                    filterMode: FilterMode.Trilinear,
                    anisoLevel: 8,
                    // msaaSamples: MSAASamples.MSAA8x,
                    name: "Overscanned Target");
            }

            return true;
        }
    }
}
