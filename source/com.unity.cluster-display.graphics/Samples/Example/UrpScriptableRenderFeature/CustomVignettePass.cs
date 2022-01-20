#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

class CustomVignettePass : ScriptableRenderPass
{
    Material m_Material;
    
    public CustomVignettePass(Material material)
    { 
        m_Material = material;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("Example Screen Coord Override");
        Blit(cmd, ref renderingData, m_Material);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
#endif
