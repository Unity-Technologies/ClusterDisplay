#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ExampleScreenCoordOverrideFeature : ScriptableRendererFeature
{
    const string k_ShaderPath = "Hidden/ExampleScreenCoordOverride";
    
    ExampleScreenCoordOverridePass m_ScriptablePass;
    Material m_Material;
    
    public override void Create()
    {
        if (m_Material == null)
        {
            m_Material = CoreUtils.CreateEngineMaterial(k_ShaderPath);
        }
        
        m_ScriptablePass = new ExampleScreenCoordOverridePass();
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }
    
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        m_ScriptablePass.Setup(m_Material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
    
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
    }
}
#endif
