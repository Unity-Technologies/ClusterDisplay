using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    interface IClusterRenderer
    {
        ClusterRenderContext Context { get; }
        ClusterRendererSettings Settings { get; }
    }
}
