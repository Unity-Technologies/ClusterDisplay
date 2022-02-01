#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A custom Scriptable Render Pass demonstrating the use of Cluster Display shading features.
/// </summary>
sealed class UrpVignettePass : ScriptableRenderPass
{
    static readonly string k_CommandBufferName = nameof(UrpVignettePass);

    /// <summary>
    /// The material used to render the pass.
    /// </summary>
    public Material Material;

    /// <inheritdoc/>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (Material == null)
        {
            return;
        }
        
        var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        var cmd = CommandBufferPool.Get(k_CommandBufferName);

        // Note the use of a temporary render target since we read then write to the camera target.
        cmd.GetTemporaryRT(UrpVignetteFeature.ShaderProperties._TempTex, descriptor);
        cmd.SetRenderTarget(UrpVignetteFeature.ShaderProperties._TempTex);
        cmd.SetGlobalTexture(UrpVignetteFeature.ShaderProperties._SourceTex, target);
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, Material, 0, 0);
        cmd.Blit(UrpVignetteFeature.ShaderProperties._TempTex, target);
        cmd.ReleaseTemporaryRT(UrpVignetteFeature.ShaderProperties._TempTex);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
#endif
