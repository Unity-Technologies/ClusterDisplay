using System;
using Unity.ClusterDisplay.Graphics;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [ExecuteAlways]
    public class OverrideCameraProjection : MonoBehaviour
    {
        private bool m_DelegateSet;

        private void Update()
        {
            if (m_DelegateSet)
                return;

            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;

            clusterRenderer.userPreCameraRenderDataOverride -= PreRenderCameraDataOverride;
            clusterRenderer.userPreCameraRenderDataOverride += PreRenderCameraDataOverride;

            m_DelegateSet = true;
        }
        
        private void PreRenderCameraDataOverride(int nodeId, ref Vector3 position, ref Quaternion rotation, ref Matrix4x4 matrix)
        {
            rotation = Quaternion.AngleAxis(0f + 45f * nodeId, Vector3.up);
            position = position + rotation * new Vector3(0f, 0f, -4f);
            matrix = Matrix4x4.Perspective(45f, Screen.height / (float)Screen.width, 0.1f, 100f);
        }

        private void OnDisable()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;
            
            clusterRenderer.userPreCameraRenderDataOverride -= PreRenderCameraDataOverride;
            m_DelegateSet = false;
        }
        
        private void OnDestroy()
        {
            if (!ClusterRenderer.TryGetInstance(out var clusterRenderer))
                return;

            clusterRenderer.userPreCameraRenderDataOverride -= PreRenderCameraDataOverride;
            m_DelegateSet = false;
        }
    }
}
