using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class LayoutBuilderUtils
    {
        static readonly int k_ClusterDisplayParams = Shader.PropertyToID("_ClusterDisplayParams");
        public static Vector4 ScaleBiasRT { get; } = new Vector4(1, 1, 0, 0);

        public static void UploadClusterDisplayParams(Matrix4x4 clusterDisplayParams)
        {
            Shader.SetGlobalMatrix(k_ClusterDisplayParams, clusterDisplayParams);
        }

        public static Vector2 CalculateCroppedSize(Rect rect, int overscanInPixels)
        {
            return new Vector2(rect.width - 2 * overscanInPixels, rect.height - 2 * overscanInPixels);
        }

        public static Vector4 CalculateScaleBias(Rect overscannedRect, int overscanInPixels, Vector2 debugOffset)
        {
            var croppedSize = new Vector2(overscannedRect.width - 2 * overscanInPixels, overscannedRect.height - 2 * overscanInPixels);
            var overscannedSize = new Vector2(overscannedRect.width, overscannedRect.height);

            var scaleBias = new Vector4(
                croppedSize.x / overscannedSize.x, croppedSize.y / overscannedSize.y, // scale
                overscanInPixels / overscannedSize.x, overscanInPixels / overscannedSize.y); // offset
            scaleBias.z += debugOffset.x;
            scaleBias.w += debugOffset.y;

            return scaleBias;
        }
        
        public static bool ValidGridSize(this IClusterRenderer clusterRenderer, out int numTiles)
        {
            numTiles = clusterRenderer.Context.GridSize.x * clusterRenderer.Context.GridSize.y;
            return numTiles > 0;
        }

        public static Rect CalculateOverscannedRect(this IClusterRenderer clusterRenderer, int width, int height)
        {
            return new Rect(0, 0,
                width + 2 * clusterRenderer.Context.OverscanInPixels,
                height + 2 * clusterRenderer.Context.OverscanInPixels);
        }
    }
}
