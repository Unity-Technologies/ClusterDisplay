using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public static class CameraExtensionMethods
    {
        public static Vector2 ScreenPointToClusterDisplayScreenPoint (this Camera camera, Vector2 screenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer) || 
                !ClusterSync.TryGetInstance(out var clusterSync) || 
                !clusterSync.TryGetDynamicLocalNodeId(out var tileId))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileId);
            return GraphicsUtil.NdcToNcc(clusterDisplayParams, screenPoint);
        }

        public static Vector2 ScreenPointToClusterDisplayScreenPoint (this Camera camera, int tileIndex, Vector2 screenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileIndex);
            return GraphicsUtil.NdcToNcc(clusterDisplayParams, screenPoint);
        }

        public static Vector2 ScreenPointToClusterDisplayWorldPoint (this Camera camera, Vector2 screenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer) || 
                !ClusterSync.TryGetInstance(out var clusterSync) || 
                !clusterSync.TryGetDynamicLocalNodeId(out var tileId))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileId);
            return camera.ScreenToWorldPoint(GraphicsUtil.NdcToNcc(clusterDisplayParams, screenPoint));
        }

        public static Vector2 ScreenPointToClusterDisplayWorldPoint (this Camera camera, int tileIndex, Vector2 screenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileIndex);
            return camera.ScreenToWorldPoint(GraphicsUtil.NdcToNcc(clusterDisplayParams, screenPoint));
        }

        public static Vector2 ClusterDisplayScreenPointToScreenPoint (this Camera camera, Vector2 clusterDisplayScreenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer) || 
                !ClusterSync.TryGetInstance(out var clusterSync) || 
                !clusterSync.TryGetDynamicLocalNodeId(out var tileId))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileId);
            return GraphicsUtil.NccToNdc(clusterDisplayParams, clusterDisplayScreenPoint);
        }

        public static Vector2 ClusterDisplayScreenPointToScreenPoint (this Camera camera, int tileIndex, Vector2 clusterDisplayScreenPoint)
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return Vector2.zero;

            var clusterDisplayParams = GraphicsUtil.CalculateClusterDisplayParams(clusterRenderer, tileIndex);
            return GraphicsUtil.NccToNdc(clusterDisplayParams, clusterDisplayScreenPoint);
        }
    }
}
