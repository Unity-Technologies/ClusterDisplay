using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Unity.ClusterDisplay.Graphics
{
    public interface IClusterRenderer
    {
        ClusterRenderContext Context { get; }
        ClusterCameraController CameraController { get; }
        ClusterRendererSettings Settings { get; }
    }

}
