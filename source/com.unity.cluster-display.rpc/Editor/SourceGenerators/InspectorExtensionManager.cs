using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay
{
    [CreateAssetMenu(fileName = "InspectorExtensionManager", menuName = "Cluster Display/Inspector Extension Manager")]
    internal class InspectorExtensionManager : SingletonScriptableObject<InspectorExtensionManager>
    {
        protected override void OnAwake()
        {
        }
    }
}
