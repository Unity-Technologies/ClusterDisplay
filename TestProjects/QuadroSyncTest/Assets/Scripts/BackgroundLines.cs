using System.Collections;
using System.Collections.Generic;
using Unity.ClusterDisplay;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BackgroundLines : MonoBehaviour
{
    private Material m_Material;

    [SerializeField][HideInInspector]
    private MeshRenderer m_MeshRenderer;

    static readonly CommandLineParser.FloatArgument linesThickness               = new CommandLineParser.FloatArgument("-linesThickness");
    static readonly CommandLineParser.FloatArgument linesScale                   = new CommandLineParser.FloatArgument("-linesScale");
    static readonly CommandLineParser.FloatArgument linesShiftSpeed              = new CommandLineParser.FloatArgument("-linesShiftSpeed");
    static readonly CommandLineParser.FloatArgument linesAngle                   = new CommandLineParser.FloatArgument("-linesAngle");
    static readonly CommandLineParser.FloatArgument linesRotationSpeed           = new CommandLineParser.FloatArgument("-linesRotationSpeed");

    private void OnValidate() => m_MeshRenderer = GetComponent<MeshRenderer>();

    private void OnEnable ()
    {
        if (m_MeshRenderer == null)
            throw new System.Exception($"No instance of {nameof(MeshRenderer)} attached to: \"{gameObject.name}");

        m_Material = m_MeshRenderer.material;
        if (m_Material == null)
            throw new System.Exception($"No material assigned to {nameof(MeshRenderer)} attached to: \"{gameObject.name}");

        if (linesThickness.Defined)
            m_Material.SetFloat("_LinesThickness", linesThickness.Value);

        if (linesScale.Defined)
            m_Material.SetFloat("_LinesScale", linesScale.Value);

        if (linesShiftSpeed.Defined)
            m_Material.SetFloat("_LinesShiftSpeed", linesShiftSpeed.Value);

        if (linesAngle.Defined)
            m_Material.SetFloat("_LinesAngle", linesAngle.Value);

        if (linesRotationSpeed.Defined)
            m_Material.SetFloat("_LinesRotationSpeed", linesRotationSpeed.Value);

        m_MeshRenderer.material = m_Material;
    }
}
