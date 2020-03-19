using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

public class BorderCustomPass : CustomPass
{
    public Vector2Int gridSize;
    public Vector2 normalizedBorder;
    public Color color;
    public Color borderColor;
    
    // To make sure the shader will end up in the build, we keep it's reference in the custom pass
    [SerializeField, HideInInspector]
    Shader m_Shader;
    Material m_Material;
    MaterialPropertyBlock m_Properties;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        m_Shader = Shader.Find("Hidden/Border");
        m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
        m_Properties = new MaterialPropertyBlock();
    }

    protected override void Execute(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera camera, CullingResults cullingResult)
    {
        SetCameraRenderTarget(cmd);

        m_Properties.SetVector("_NormalizedBorder", normalizedBorder);
        m_Properties.SetVector("_GridSize", new Vector2(gridSize.x, gridSize.y));
        m_Properties.SetColor("_Color", color);
        m_Properties.SetColor("_BorderColor", borderColor);
        CoreUtils.DrawFullScreen(cmd, m_Material, m_Properties, shaderPassId: 0);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
    }
}