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
            rotation = Quaternion.AngleAxis(0f + 45f * nodeId, Vector3.up);
            position = position + rotation * new Vector3(0f, 0f, -4f);
            matrix = Matrix4x4.Perspective(45f, Screen.height / (float)Screen.width, 0.1f, 100f);
        }

        private void OnEnable()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;
            
            clusterRenderer.preRenderCameraDataOverride -= PreRenderCameraDataOverride;
            clusterRenderer.preRenderCameraDataOverride += PreRenderCameraDataOverride;
        }

        private void OnDisable()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;
            
            clusterRenderer.preRenderCameraDataOverride -= PreRenderCameraDataOverride;
        }
        
        private void OnDestroy()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer, throwError: false))
                return;
            clusterRenderer.preRenderCameraDataOverride -= PreRenderCameraDataOverride;
        }
    }
}
