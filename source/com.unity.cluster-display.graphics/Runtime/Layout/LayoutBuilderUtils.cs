using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class LayoutBuilderUtils
    {
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");

        public static void UploadClusterDisplayParams(Matrix4x4 clusterDisplayParams)
        {
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterDisplayParams);
        }

        public static Vector2 CalculateCroppedSize(Vector2 overscannedSize, int overscanInPixels)
        {
            return new Vector2(overscannedSize.x - 2 * overscanInPixels, overscannedSize.y - 2 * overscanInPixels);
        }
        
        public static Vector2 CalculateOverscannedSize(int width, int height, int overscanInPixels)
        {
            return new Vector2(width + 2 * overscanInPixels, height + 2 * overscanInPixels);
        }

        public static Vector4 CalculateScaleBias(Vector2 overscannedSize, int overscanInPixels, Vector2 debugOffset)
        {
            var croppedSize = new Vector2(overscannedSize.x - 2 * overscanInPixels, overscannedSize.y - 2 * overscanInPixels);

            var scaleBias = new Vector4(
                croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                overscanInPixels / overscannedSize.x, overscanInPixels / overscannedSize.y); // offset
            scaleBias.z += debugOffset.x;
            scaleBias.w += debugOffset.y;

            return scaleBias;
        }
        
        public static void GetViewportAndProjection(
            ClusterRenderContext context,
            Matrix4x4 projectionMatrix,
            int tileIndex,
            out Matrix4x4 asymmetricProjectionMatrix,
            out Rect viewportSubsection)
        {
            viewportSubsection = GraphicsUtil.TileIndexToViewportSection(context.GridSize, tileIndex);
            
            if (context.PhysicalScreenSize != Vector2Int.zero && context.Bezel != Vector2Int.zero)
            {
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, context.PhysicalScreenSize, context.Bezel);
            }

            viewportSubsection = GraphicsUtil.ApplyOverscan(viewportSubsection, context.OverscanInPixels);

            asymmetricProjectionMatrix = GraphicsUtil.GetFrustumSlicingAsymmetricProjection(projectionMatrix, viewportSubsection);
        }
        
        public static float GetAspect(ClusterRenderContext context, int screenWidth, int screenHeight) => context.GridSize.x * screenWidth / (float)(context.GridSize.y * screenHeight);
    }
}
