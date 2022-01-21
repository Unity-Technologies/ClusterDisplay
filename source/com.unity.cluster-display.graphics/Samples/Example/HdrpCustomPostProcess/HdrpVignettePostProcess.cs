#if CLUSTER_DISPLAY_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

/// <summary>
/// An example custom Post Process demonstrating the use of Cluster Display shading features.
/// </summary>
[Serializable, VolumeComponentMenu("Post-processing/ClusterDisplay/Samples/Vignette")]
public sealed class HdrpVignettePostProcess : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    static class ShaderProperties
    {
        public static readonly int _Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int _Color = Shader.PropertyToID("_Color");
    }

    /// <summary>
    /// The intensity of the effect.
    /// </summary>
    [Tooltip("Controls the intensity of the effect.")]
    public ClampedFloatParameter Intensity = new ClampedFloatParameter(0f, 0f, 1f);

    /// <summary>
    /// The color of the vignette.
    /// </summary>
    [Tooltip("Controls the color of the vignette.")]
    public ColorParameter Color = new ColorParameter(UnityEngine.Color.black);

    Material m_Material;

    /// <inheritdoc/>
    public bool IsActive() => m_Material != null && Intensity.value > 0f;

    // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Settings).
    /// <inheritdoc/>
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    const string k_ShaderPath = "ClusterDisplay/Samples/HDRP/CustomPostProcess/Vignette";

    /// <inheritdoc/>
    public override void Setup()
    {
        m_Material = CoreUtils.CreateEngineMaterial(k_ShaderPath);

        if (!Application.isEditor && m_Material == null)
        {
            Debug.LogError($"Make sure shader \"{k_ShaderPath}\" has been added to the list of always included shaders.");
        }
    }

    /// <inheritdoc/>
    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        m_Material.SetFloat(ShaderProperties._Intensity, Intensity.value);
        m_Material.SetColor(ShaderProperties._Color, Color.value);
        cmd.Blit(source, destination, m_Material, 0);
    }

    /// <inheritdoc/>
    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}
#endif
