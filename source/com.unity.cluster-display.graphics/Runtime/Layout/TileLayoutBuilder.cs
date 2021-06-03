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
            viewportSubsection = k_ClusterRenderer.context.GetViewportSubsection();
            if (k_ClusterRenderer.context.physicalScreenSize != Vector2Int.zero && k_ClusterRenderer.context.bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.context.physicalScreenSize, k_ClusterRenderer.context.bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.context.overscanInPixels);

            projectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(k_ClusterRenderer.cameraController.CacheAndReturnProjectionMatrix(), viewportSubsection);
            
            return true;
        }
    }
}
