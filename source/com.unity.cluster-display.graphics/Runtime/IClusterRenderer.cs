using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface IClusterRenderer
    {
        ClusterRenderContext context { get; }
        ClusterCameraController cameraController { get; }
        ClusterRendererSettings settings { get; }
    }
}
