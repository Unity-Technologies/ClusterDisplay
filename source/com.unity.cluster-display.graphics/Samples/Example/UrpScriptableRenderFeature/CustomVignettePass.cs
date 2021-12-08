#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

class CustomVignettePass : ScriptableRenderPass
{
    Material m_Material;
    
    public void Setup(Material material) => m_Material = material;

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cameraType = renderingData.cameraData.camera.cameraType;
        if (cameraType == CameraType.Preview | cameraType == CameraType.SceneView)
        {
            return;
        }

        var cmd = CommandBufferPool.Get("Example Screen Coord Override");
        Blit(cmd, ref renderingData, m_Material);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
#endif
