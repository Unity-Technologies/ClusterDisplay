using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public static class GraphicsUtil
    {
        public static Matrix4x4 GetClusterDisplayParams(Rect overscannedViewportSubsection, Vector2 globalScreenSize, Vector2Int gridSize)
        {
            var parms = new Matrix4x4();

            var translationAndScale = new Vector4(overscannedViewportSubsection.x, overscannedViewportSubsection.y, overscannedViewportSubsection.width, overscannedViewportSubsection.height);
            parms.SetRow(0, translationAndScale);
            
            var screenSize = new Vector4(globalScreenSize.x, globalScreenSize.y, 1.0f / globalScreenSize.x, 1.0f / globalScreenSize.y);
            parms.SetRow(1, screenSize);
            
            var grid = new Vector4(gridSize.x, gridSize.y, 0, 0);
            parms.SetRow(2, grid);
            
            return parms;
        }

        // there's no *right* way to do it, it simply is a convention
        public static Rect TileIndexToViewportSection(Vector2Int gridSize, int tileIndex)
        {
            if (gridSize.x * gridSize.y == 0)
                return Rect.zero;
            var x = tileIndex % gridSize.x;
            var y = gridSize.y - 1 - tileIndex / gridSize.x; // tile 0 is top-left
            var dx = 1f / (float) gridSize.x;
            var dy = 1f / (float) gridSize.y;
            return new Rect(x * dx, y * dy, dx, dy);
        }
        
        static Rect Expand(Rect r, Vector2 delta)
        {
            return Rect.MinMaxRect(
                r.min.x - delta.x,
                r.min.y - delta.y,
                r.max.x + delta.x,
                r.max.y + delta.y);
        }

        public static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels)
        {
            return ApplyOverscan(normalizedViewportSubsection, overscanInPixels, Screen.width, Screen.height);
        }

        public static Rect ApplyOverscan(Rect normalizedViewportSubsection, int overscanInPixels, int viewportWidth, int viewportHeight)
        {
            var normalizedOverscan = new Vector2(
                overscanInPixels * (normalizedViewportSubsection.max.x - normalizedViewportSubsection.min.x) / viewportWidth,
                overscanInPixels * (normalizedViewportSubsection.max.y - normalizedViewportSubsection.min.y) / viewportHeight);

            return Expand(normalizedViewportSubsection, normalizedOverscan);
        }
        
        public static Rect ApplyBezel(Rect normalizedViewportSubsection, Vector2 physicalScreenSizeInMm, Vector2 bezelInMm)
        {
            var normalizedBezel = new Vector2(
                bezelInMm.x / (float)physicalScreenSizeInMm.x, 
                bezelInMm.y / (float)physicalScreenSizeInMm.y);
            
            var bezel = new Vector2(
                normalizedViewportSubsection.width * normalizedBezel.x,
                normalizedViewportSubsection.height * normalizedBezel.y);

            return Rect.MinMaxRect(
                normalizedViewportSubsection.min.x + bezel.x,
                normalizedViewportSubsection.min.y + bezel.y,
                normalizedViewportSubsection.max.x - bezel.x,
                normalizedViewportSubsection.max.y - bezel.y);
        }
        
        public static Matrix4x4 GetFrustumSlicingAsymmetricProjection(Matrix4x4 originalProjection, Rect normalizedViewportSubsection)
        {
            var baseFrustumPlanes = originalProjection.decomposeProjection;
            var frustumPlanes = new FrustumPlanes();
            frustumPlanes.zNear  = baseFrustumPlanes.zNear;
            frustumPlanes.zFar   = baseFrustumPlanes.zFar;
            frustumPlanes.left   = Mathf.LerpUnclamped(baseFrustumPlanes.left,   baseFrustumPlanes.right, normalizedViewportSubsection.xMin);
            frustumPlanes.right  = Mathf.LerpUnclamped(baseFrustumPlanes.left,   baseFrustumPlanes.right, normalizedViewportSubsection.xMax);
            frustumPlanes.bottom = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top,   normalizedViewportSubsection.yMin);
            frustumPlanes.top    = Mathf.LerpUnclamped(baseFrustumPlanes.bottom, baseFrustumPlanes.top,   normalizedViewportSubsection.yMax);
            return Matrix4x4.Frustum(frustumPlanes);
        }

        public static Rect CalculateNormalizedViewport (ClusterRenderer clusterRenderer, int tileId)
        {
            var viewportSubsection = clusterRenderer.context.GetViewportSubsection(tileId);
            if (clusterRenderer.context.physicalScreenSize != Vector2Int.zero && clusterRenderer.context.bezel != Vector2Int.zero)
                viewportSubsection = GraphicsUtil.ApplyBezel(viewportSubsection, clusterRenderer.context.physicalScreenSize, clusterRenderer.context.bezel);
            return GraphicsUtil.ApplyOverscan(viewportSubsection, clusterRenderer.context.overscanInPixels);
        }

        public static Matrix4x4 CalculateClusterDisplayParams (int tileId)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return Matrix4x4.identity;

            var viewportSubsection = CalculateNormalizedViewport(clusterRenderer, tileId);
            return GraphicsUtil.GetClusterDisplayParams(viewportSubsection, clusterRenderer.context.globalScreenSize, clusterRenderer.context.gridSize);

        }

        /// <summary>
        /// Calculate the render size with overscan for post processing.
        /// </summary>
        /// <param name="overscanInPixels"></param>
        /// <returns></returns>
        public static Rect CalculateOverscannedRect (int overscanInPixels) =>
            new Rect(0, 0, 
                Screen.width + 2 * overscanInPixels, 
                Screen.height + 2 * overscanInPixels);

        public static Vector2 DeviceToClusterFullscreenUV(Matrix4x4 clusterDisplayParams, Vector2 xy) =>
            new Vector2(clusterDisplayParams[0, 0], clusterDisplayParams[0, 1]) + new Vector2(xy.x * clusterDisplayParams[0, 2], xy.y * clusterDisplayParams[0, 3]);

        public static Vector2 ClusterToDeviceFullscreenUV(Matrix4x4 clusterDisplayParams, Vector2 xy) =>
            new Vector2(xy.x - clusterDisplayParams[0, 0], xy.y - clusterDisplayParams[0, 1]) / new Vector2(clusterDisplayParams[0, 2], clusterDisplayParams[0, 3]);

        // NDC = normalized device coordinates
        // NCC = normalized cluster coordinates.

        public static Vector2 NdcToNcc(Matrix4x4 clusterDisplayParams, Vector2 xy)
        {
            // ndc to device-uv
            xy.y = -xy.y;
            xy = (xy + Vector2.one) * 0.5f;
            xy = DeviceToClusterFullscreenUV(clusterDisplayParams, xy);
            // cluster-UV to ncc
            xy = xy * 2 - Vector2.one;
            xy.y = -xy.y;
            return xy;
        }

        public static Vector2 NccToNdc(Matrix4x4 clusterDisplayParams, Vector2 xy)
        {
            // ncc to cluster-UV
            xy.y = -xy.y;
            xy = (xy + Vector2.one) * 0.5f;
            xy = ClusterToDeviceFullscreenUV(clusterDisplayParams, xy);
            // device-UV to ndc
            xy = xy * 2 - Vector2.one;
            xy.y = -xy.y;
            return xy;
        }
    }
}
