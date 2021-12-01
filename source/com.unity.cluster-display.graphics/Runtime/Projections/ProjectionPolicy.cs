using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    [DisallowMultipleComponent]
    public abstract class ProjectionPolicy : MonoBehaviour
    {
        public abstract void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera);
        public abstract void Present(CommandBuffer commandBuffer);
    }
}
