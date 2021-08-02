using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

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
    public class HierarchyTransformStream : MonoBehaviour
    {
        private Transform[] cachedChildTransforms;
        private Transform[] CachedChildTransforms
        {
            get
            {
                if (cachedChildTransforms == null)
                    cachedChildTransforms = GetComponentsInChildren<Transform>();

                return cachedChildTransforms;
            }
        }

        public int TransformCount => CachedChildTransforms == null ? 1 : CachedChildTransforms.Length + 1;
        private NativeArray<Data> cachedTransformData;

        private void Test (NativeArray<Data> test)
        {
            if (test.Length == 0)
                Debug.Log("TEST");

            RPCEmitter.AppendRPCNativeArrayParameterValues<Data>(test);
        }

        private void CacheHierarchyTransformations ()
        {
            if (cachedTransformData == null || cachedTransformData.Length != CachedChildTransforms.Length)
                cachedTransformData = new NativeArray<Data>(CachedChildTransforms.Length, Allocator.Persistent);

            for (int i = 0; i < CachedChildTransforms.Length; i++)
                cachedTransformData[i] = new Data
                {
                    localPosition = CachedChildTransforms[i].localPosition,
                    localRotation = CachedChildTransforms[i].localRotation,
                    localScale = CachedChildTransforms[i].localScale
                };
        }

        private void LateUpdate ()
        {
            if (!ClusterDisplayState.IsMaster)
                return;

            CacheHierarchyTransformations();
            ApplyHierarchyTransformations(cachedTransformData);
        }

        [ClusterRPC(RPCExecutionStage.AfterUpdate)]
        public void ApplyHierarchyTransformations (NativeArray<Data> transformations)
        {
            if (ClusterDisplayState.IsMaster)
                return;

            for (int i = 0; i < CachedChildTransforms.Length; i++)
            {
                CachedChildTransforms[i].localPosition = transformations[i].localPosition;
                CachedChildTransforms[i].localRotation = transformations[i].localRotation;
                CachedChildTransforms[i].localScale = transformations[i].localScale;
            }
        }
    }
}
