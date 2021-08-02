using Unity.Collections;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Transform))]
    public class FilteredTransformHierarchyStream : TransformHierarchyStreamBase
    {
        protected override void CacheTransforms()
        {
        }
    }
}
