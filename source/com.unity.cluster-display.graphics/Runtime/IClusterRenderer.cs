using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface IClusterRenderer
    {
        ClusterRenderContext Context { get; }
        ClusterCameraController CameraController { get; }
        ClusterRendererSettings Settings { get; }
    }
}
