using Unity.Collections;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    public struct Data
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [RequireComponent(typeof(Transform))]
    public class SimpleTransformHierarchyStream : TransformHierarchyStreamBase
    {
        protected override void CacheTransforms() => ApplyCachedTransforms(GetComponentsInChildren<Transform>());

        [ClusterRPC] // We want the RPCs for methods in non-abstract classes, therefore we declare this method as an RPC, then call up to the base implementation.
        public override void ApplyTransformData (NativeArray<Data> data) => base.ApplyTransformData(data);
    }
}
