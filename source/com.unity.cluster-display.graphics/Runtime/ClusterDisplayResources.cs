﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [CreateAssetMenu(fileName = "Data", menuName = "Cluster Display/ClusterDisplayResources", order = 1)]
    public class ClusterDisplayResources : ScriptableObject
    {
        [SerializeField] private Material m_BlitMaterial;
        public Material blitMaterial => m_BlitMaterial;
    }
}
