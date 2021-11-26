using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    public interface IProjectionPolicy
    {
        public void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera);
        public void Present(CommandBuffer commandBuffer);
    }
}
