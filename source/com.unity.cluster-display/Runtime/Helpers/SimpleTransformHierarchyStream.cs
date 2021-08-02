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
        private void LateUpdate ()
        {
            if (!ClusterDisplayState.IsMaster)
                return;

            if (!TryGetData(out var data))
                return;

            ApplyTransformData(data);
        }

        protected override void CacheTransforms() => ApplyCachedTransforms(GetComponentsInChildren<Transform>());

        [ClusterRPC] // We want the RPCs for method instances, therefore we declare this method as an RPC, then call up to the base implementation.
        public override void ApplyTransformData (NativeSlice<Data> data) => base.ApplyTransformData(data);
    }
}
