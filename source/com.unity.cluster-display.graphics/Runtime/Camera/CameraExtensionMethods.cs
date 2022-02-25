using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public static class CameraExtensionMethods
    {
        // NDC = Normalized device coordinates.
        // NCC = Normalized cluster coordinates.
        
        /*
        private static Vector2 DeviceToClusterFullscreenUV(Matrix4x4 clusterDisplayParams, Vector2 xy) =>
            new Vector2(clusterDisplayParams[0, 0], clusterDisplayParams[0, 1]) + new Vector2(xy.x * clusterDisplayParams[0, 2], xy.y * clusterDisplayParams[0, 3]);

        private static Vector2 ClusterToDeviceFullscreenUV(Matrix4x4 clusterDisplayParams, Vector2 xy) =>
            new Vector2(xy.x - clusterDisplayParams[0, 0], xy.y - clusterDisplayParams[0, 1]) / new Vector2(clusterDisplayParams[0, 2], clusterDisplayParams[0, 3]);
        
        private static Vector2 NdcToNcc(Matrix4x4 clusterDisplayParams, Vector2 xy)
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

        private static Vector2 NccToNdc(Matrix4x4 clusterDisplayParams, Vector2 xy)
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
        
        
        public static Vector2 NDCToNCC(this Camera camera, ProjectionPolicy projectionPolicy, Vector2 ndc) =>
            NdcToNcc(GraphicsUtil.CalculateClusterDisplayParams(ClusterDisplayState.NodeID), ndc);

        public static Vector2 NCCToNDC (this Camera camera, Vector2 ncc) =>
            NccToNdc(GraphicsUtil.CalculateClusterDisplayParams(ClusterDisplayState.NodeID), ncc);

        public static Vector2 NCCToClusterScreenPosition (this Camera camera, Vector2 ncc) =>
            new Vector2(Screen.width * (ncc.x * 0.5f + 0.5f), Screen.height * (ncc.y * 0.5f + 0.5f));

        public static Vector2 NDCToDeviceScreenPosition (this Camera camera, Vector2 ndc) =>
            new Vector2(Screen.width * (ndc.x * 0.5f + 0.5f), Screen.height * (ndc.y * 0.5f + 0.5f));

        public static Vector2 DeviceScreenPositionToNCC (this Camera camera, ProjectionPolicy projectionPolicy, Vector2 deviceScreenPosition) =>
            NDCToNCC(camera, projectionPolicy, DeviceScreenPositionToNDC(camera, deviceScreenPosition));

        public static Vector2 DeviceScreenPositionToNDC (this Camera camera, Vector2 deviceScreenPosition) =>
            new Vector2(deviceScreenPosition.x / Screen.width, deviceScreenPosition.y / Screen.height) * 2f - Vector2.one;

        public static Vector2 DeviceScreenPositionToClusterScreenPosition(this Camera camera, ProjectionPolicy projectionPolicy, Vector2 deviceScreenPosition) =>
            NCCToClusterScreenPosition(camera, NDCToNCC(camera, projectionPolicy, DeviceScreenPositionToNDC(camera, deviceScreenPosition)));

        public static Vector2 ClusterScreenPositionToDeviceScreenPosition(this Camera camera, ProjectionPolicy projectionPolicy, Vector2 clusterScreenPosition) =>
            NCCToClusterScreenPosition(camera, DeviceScreenPositionToNCC(camera, projectionPolicy, clusterScreenPosition));
        */
    }
}
