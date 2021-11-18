using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    public static class CameraExtensionMethods
    {
        // NDC = Normalized device coordinates.
        // NCC = Normalized cluster coordinates.

        public static Vector2 NDCToNCC (this Camera camera, Vector2 ndc) =>
            GraphicsUtil.NdcToNcc(GraphicsUtil.CalculateClusterDisplayParams(ClusterDisplayState.NodeID), ndc);

        public static Vector2 NCCToNDC (this Camera camera, Vector2 ncc) =>
            GraphicsUtil.NccToNdc(GraphicsUtil.CalculateClusterDisplayParams(ClusterDisplayState.NodeID), ncc);

        public static Vector2 NCCToClusterScreenPosition (this Camera camera, Vector2 ncc) =>
            new Vector2(Screen.width * (ncc.x * 0.5f + 0.5f), Screen.height * (ncc.y * 0.5f + 0.5f));

        public static Vector2 NDCToDeviceScreenPosition (this Camera camera, Vector2 ndc) =>
            new Vector2(Screen.width * (ndc.x * 0.5f + 0.5f), Screen.height * (ndc.y * 0.5f + 0.5f));

        public static Vector2 DeviceScreenPositionToNCC (this Camera camera, Vector2 deviceScreenPosition) =>
            NDCToNCC(camera, DeviceScreenPositionToNDC(camera, deviceScreenPosition));

        public static Vector2 DeviceScreenPositionToNDC (this Camera camera, Vector2 deviceScreenPosition) =>
            new Vector2(deviceScreenPosition.x / Screen.width, deviceScreenPosition.y / Screen.height) * 2f - Vector2.one;

        public static Vector2 DeviceScreenPositionToClusterScreenPosition(this Camera camera, Vector2 deviceScreenPosition) =>
            NCCToClusterScreenPosition(camera, NDCToNCC(camera, DeviceScreenPositionToNDC(camera, deviceScreenPosition)));

        public static Vector2 ClusterScreenPositionToDeviceScreenPosition(this Camera camera, Vector2 clusterScreenPosition) =>
            NCCToClusterScreenPosition(camera, DeviceScreenPositionToNCC(camera, clusterScreenPosition));
    }
}
