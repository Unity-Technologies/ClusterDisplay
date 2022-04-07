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

    private void OnValidate() => m_MeshRenderer = GetComponent<MeshRenderer>();

    private void OnEnable ()
    {
        if (m_MeshRenderer == null)
            throw new System.Exception($"No instance of {nameof(MeshRenderer)} attached to: \"{gameObject.name}");

        m_Material = m_MeshRenderer.material;
        if (m_Material == null)
            throw new System.Exception($"No material assigned to {nameof(MeshRenderer)} attached to: \"{gameObject.name}");

        if (CommandLineParser.linesThickness.Defined)
            m_Material.SetFloat("_LinesThickness", CommandLineParser.linesThickness.Value);

        if (CommandLineParser.linesScale.Defined)
            m_Material.SetFloat("_LinesScale", CommandLineParser.linesScale.Value);

        if (CommandLineParser.linesShiftSpeed.Defined)
            m_Material.SetFloat("_LinesShiftSpeed", CommandLineParser.linesShiftSpeed.Value);

        if (CommandLineParser.linesAngle.Defined)
            m_Material.SetFloat("_LinesAngle", CommandLineParser.linesAngle.Value);

        if (CommandLineParser.linesRotationSpeed.Defined)
            m_Material.SetFloat("_LinesRotationSpeed", CommandLineParser.linesRotationSpeed.Value);

        m_MeshRenderer.material = m_Material;
    }
}
