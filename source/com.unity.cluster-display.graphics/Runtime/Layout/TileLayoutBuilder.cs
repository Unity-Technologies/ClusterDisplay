using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public abstract class TileLayoutBuilder : LayoutBuilder
    {
        protected TileLayoutBuilder(IClusterRenderer clusterRenderer) : base(clusterRenderer) {}

        protected bool SetupTiledLayout (
            Camera camera, 
            out Matrix4x4 asymmetricProjectionMatrix,
            out Rect viewportSubsection,
            out Rect overscannedRect)
        {
            if (!ValidGridSize(out var numTiles) || camera.cameraType != CameraType.Game)
            {
                asymmetricProjectionMatrix = Matrix4x4.identity;
                viewportSubsection = Rect.zero;
                overscannedRect = Rect.zero;
                return false;
            }

            overscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            viewportSubsection = k_ClusterRenderer.context.GetViewportSubsection();
            if (k_ClusterRenderer.context.physicalScreenSize != Vector2Int.zero && k_ClusterRenderer.context.bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.context.physicalScreenSize, k_ClusterRenderer.context.bezel);
            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.context.overscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            return true;
        }
    }
}
