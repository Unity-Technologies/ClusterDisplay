using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileLayoutBuilder : LayoutBuilder
    {
#if CLUSTER_DISPLAY_XR
        protected TileRTManager m_RTManager = new XRTileRTManager();
#else
        protected TileRTManager m_RTManager = new StandardTileRTManager();
#endif

        protected TileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) 
        {
        }

        protected void PollRT (Camera camera, ref RenderTexture targetRT, int width, int height)
        {
        }

        protected bool SetupTiledLayout (
            Camera camera, 
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
            
            return true;
        }
    }
}
