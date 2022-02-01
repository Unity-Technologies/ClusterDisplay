#if CLUSTER_DISPLAY_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A custom Scriptable Render Feature demonstrating the use of Cluster Display shading features.
/// </summary>
public sealed class UrpVignetteFeature : ScriptableRendererFeature
{
    internal static class ShaderProperties
    {
        public static readonly int _Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int _Color = Shader.PropertyToID("_Color");
        public static readonly int _SourceTex = Shader.PropertyToID("_SourceTex");
        public static readonly int _TempTex = Shader.PropertyToID("_TempTex");
    }
    
    const string k_ShaderPath = "Hidden/ClusterDisplay/Samples/URP/Vignette";

    [SerializeField]
    RenderPassEvent m_RenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField, Range(0, 1)]
    float m_Intensity;

    [SerializeField]
    Color m_Color = Color.black;

    UrpVignettePass m_ScriptablePass;
    Material m_Material;

    Material GetMaterial()
    {
        if (m_Material == null)
        {
            m_Material = CoreUtils.CreateEngineMaterial(k_ShaderPath);

            if (!Application.isEditor && m_Material == null)
            {
                Debug.LogError($"Make sure shader \"{k_ShaderPath}\" has been added to the list of always included shaders.");
            }
        }

        m_Material.SetFloat(ShaderProperties._Intensity, m_Intensity);
        m_Material.SetColor(ShaderProperties._Color, m_Color);

        return m_Material;
    }

    UrpVignettePass GetScriptablePass()
    {
        if (m_ScriptablePass == null)
        {
            m_ScriptablePass = new UrpVignettePass();
            m_ScriptablePass.renderPassEvent = m_RenderPassEvent;
        }

        return m_ScriptablePass;
    }

    /// <inheritdoc/>
    public override void Create()
    {
    }

    /// <inheritdoc/>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // We do not want the vignette to be rendered in the scene view.
        if (renderingData.cameraData.camera.cameraType == CameraType.Game)
        {
            var pass = GetScriptablePass();
            pass.Material = GetMaterial();
            renderer.EnqueuePass(pass);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
    }
}
#endif
