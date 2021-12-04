#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

class ExampleScreenCoordOverridePass : ScriptableRenderPass
{
    static class ShaderConstants
    {
        public static readonly int _SourceTex = Shader.PropertyToID("_SourceTex");
    }

    static readonly ProfilingSampler k_ProfilingSampler = new ProfilingSampler("Example Screen Coord Override Pass");

    Material m_Material;

    public void Setup(Material material)
    {
        m_Material = material;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_Material);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
#endif
