using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomRenderPass : ScriptableRenderPass
{
    static class ShaderProperties
    {
        public static readonly int _SourceTex = Shader.PropertyToID("_SourceTex");
        public static readonly int _TempTex = Shader.PropertyToID("_TempTex");
    }

    const string k_ShaderPath = "Hidden/Test/CustomRenderPass";
    const string k_CommandBufferName = "Screen Coord Override";

    Material m_Material;

    public CustomRenderPass(RenderPassEvent renderPassEvent)
    {
        this.renderPassEvent = renderPassEvent;
        m_Material = CoreUtils.CreateEngineMaterial(k_ShaderPath);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // The API implied lifecycle has proved unreliable.
        if (m_Material == null)
        {
            return;
        }

        var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        var cmd = CommandBufferPool.Get(k_CommandBufferName);

        cmd.GetTemporaryRT(ShaderProperties._TempTex, descriptor);
        cmd.SetRenderTarget(ShaderProperties._TempTex);

        Blitter.BlitTexture(cmd, target, new Vector4(1, 1, 0, 0), m_Material, 0);

        cmd.Blit(ShaderProperties._TempTex, target);
        cmd.ReleaseTemporaryRT(ShaderProperties._TempTex);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}
