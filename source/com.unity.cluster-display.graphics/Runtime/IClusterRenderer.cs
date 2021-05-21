using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Unity.ClusterDisplay.Graphics
{
    public interface IClusterRenderer
    {
        ClusterRenderContext context { get; }
        ClusterCameraController cameraController { get; }
        ClusterRendererSettings settings { get; }
    }

}
