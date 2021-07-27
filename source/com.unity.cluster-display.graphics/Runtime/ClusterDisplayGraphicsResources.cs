﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This scriptable object can be thought of as an (X)RP RenderPipelineAsset for configuration
/// but for cluster display. There is a default instance of this scriptable object inside
/// the cluster display package that automatically gets loaded when the ClusterRenderer
/// class is initialized.
/// </summary>
[CreateAssetMenu(fileName = "Data", menuName = "Cluster Display/ClusterDisplayGraphicsResources", order = 1)]
public class ClusterDisplayGraphicsResources : ScriptableObject
{
    [SerializeField] private Material m_BlitMaterial;

    /// <summary>
    /// The material used by the standard stitcher/tile layout mode to blit a camera
    /// render into the present render texture.
    /// </summary>
    public Material blitMaterial => m_BlitMaterial;
}