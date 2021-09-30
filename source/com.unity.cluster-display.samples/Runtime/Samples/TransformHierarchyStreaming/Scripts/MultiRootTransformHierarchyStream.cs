using System.Linq;
using Unity.Collections;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    public class MultiRootTransformHierarchyStream : TransformHierarchyStreamBase
    {
        [SerializeField] private Transform[] rootTransforms = null;

        private void Awake()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        }

        [ClusterRPC]
        public override void ApplyTransformData(NativeArray<Data> data) => base.ApplyTransformData(data);
        protected override void CacheTransforms() => 
            ApplyCachedTransforms(
                rootTransforms.Concat(
                rootTransforms
                    .Where(rootTransform => rootTransform != null)
                    .SelectMany(rootTransform => rootTransform
                        .GetComponentsInChildren<Transform>()))
                .ToArray());
    }
}
