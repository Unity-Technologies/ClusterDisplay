#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomVignetteFeature : ScriptableRendererFeature
{
    static class ShaderProperties
    {
        public static readonly int _Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int _VignetteTex = Shader.PropertyToID("_VignetteTex");
        public static readonly int _VignetteColor = Shader.PropertyToID("_VignetteColor");
    }
    
    const string k_ShaderPath = "Hidden/Custom/Vignette";

    [SerializeField]
    RenderPassEvent m_RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField, Range(0, 1)]
    float m_Intensity;

    [SerializeField]
    Texture m_VignetteTex;

    [SerializeField]
    Color m_VignetteColor;
    
    CustomVignettePass m_ScriptablePass;
    Material m_Material;

    public override void Create()
    {
        if (m_Material == null)
        {
            m_Material = CoreUtils.CreateEngineMaterial(k_ShaderPath);
        }
        
        m_Material.SetFloat(ShaderProperties._Intensity, m_Intensity);
        m_Material.SetTexture(ShaderProperties._VignetteTex, m_VignetteTex);
        m_Material.SetColor(ShaderProperties._VignetteColor, m_VignetteColor);

        m_ScriptablePass = new CustomVignettePass();
        m_ScriptablePass.renderPassEvent = m_RenderPassEvent;
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
