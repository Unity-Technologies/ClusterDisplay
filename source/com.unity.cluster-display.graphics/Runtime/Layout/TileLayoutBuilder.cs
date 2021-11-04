using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    abstract class TileLayoutBuilder : LayoutBuilder
    {
        protected TileLayoutBuilder(IClusterRenderer clusterRenderer)
            : base(clusterRenderer) { }

        protected bool SetupTiledLayout(
            Camera camera,
            out Matrix4x4 asymmetricProjectionMatrix,
            out Rect viewportSubsection,
            out Rect overscannedRect)
        {
            if (!ValidGridSize(out _) || camera.cameraType != CameraType.Game)
            {
                asymmetricProjectionMatrix = Matrix4x4.identity;
                viewportSubsection = Rect.zero;
                overscannedRect = Rect.zero;
                return false;
            }

            overscannedRect = CalculateOverscannedRect(Screen.width, Screen.height);
            viewportSubsection = k_ClusterRenderer.Context.GetViewportSubsection();
            if (k_ClusterRenderer.Context.PhysicalScreenSize != Vector2Int.zero && k_ClusterRenderer.Context.Bezel != Vector2Int.zero)
            {
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, k_ClusterRenderer.Context.PhysicalScreenSize, k_ClusterRenderer.Context.Bezel);
            }

            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, k_ClusterRenderer.Context.OverscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(camera.projectionMatrix, viewportSubsection);
            return true;
        }
    }
}
