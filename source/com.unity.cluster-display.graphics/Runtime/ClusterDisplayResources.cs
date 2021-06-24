using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    [CreateAssetMenu(fileName = "Data", menuName = "Cluster Display/ClusterDisplayResources", order = 1)]
    public class ClusterDisplayResources : ScriptableObject
    {
        #pragma warning disable 0649
        [SerializeField] private Material m_BlitMaterial;
        #pragma warning restore 0649
        public Material blitMaterial => m_BlitMaterial;
    }
}
