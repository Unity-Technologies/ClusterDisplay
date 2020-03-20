using System;
using System.Collections;
using System.Collections.Generic;
using Unity.ClusterRendering.Toolkit;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
[RequireComponent(typeof(HDAdditionalCameraData))]
public class SwitchAntiAliasing : MonoBehaviour
{
    HDAdditionalCameraData m_CameraData;

    static readonly HDAdditionalCameraData.AntialiasingMode[] k_Modes = new []
    {
        HDAdditionalCameraData.AntialiasingMode.None,
        HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing,
        HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing,
        HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing
    };
    
    static readonly string[] k_ModeLabels = new [] { "none", "TAA", "FXAA", "SMAA" };
    
    void OnEnable()
    {
        m_CameraData = GetComponent<HDAdditionalCameraData>();
    }

    // introduced keyboard controls to make up for lack of imgui support with cluster display
    void Update()
    {
        var selected = Array.IndexOf(k_Modes, m_CameraData.antialiasing);
        if (Input.GetKey(KeyCode.A))
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
                ++selected;
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                --selected;
            else return;
        }

        m_CameraData.antialiasing = k_Modes[(selected + k_Modes.Length) % k_Modes.Length];
    }
    
    public void DrawGUI()
    {
        GUILayout.Label("<b>Anti Aliasing</b>");        
        GUILayout.Label("Press <b>[A]</b> then use <b>left/right</b> arrows to swap antialiasing");
        var prevSelected = Array.IndexOf(k_Modes, m_CameraData.antialiasing);
        var selected = GUILayout.SelectionGrid(prevSelected, k_ModeLabels, 2);
        if (selected != prevSelected)     
            m_CameraData.antialiasing = k_Modes[(selected + k_Modes.Length) % k_Modes.Length];
    }
}
