using System;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [ExecuteAlways]
    public class OverrideCameraProjection : MonoBehaviour
    {
        private void Start()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;

            clusterRenderer.preRenderCameraDataOverride -= PreRenderCameraDataOverride;
            clusterRenderer.preRenderCameraDataOverride += PreRenderCameraDataOverride;
        }

        private void PreRenderCameraDataOverride(int nodeId, ref Vector3 position, ref Quaternion rotation, ref Matrix4x4 matrix)
        {
            position.x += nodeId - 0.5f;
            rotation = Quaternion.AngleAxis((nodeId - 0.5f) * 45f, Vector3.up);
            matrix = Matrix4x4.Perspective(45f, 16 / 9f, 0.1f, 100f);
        }
    }
}
