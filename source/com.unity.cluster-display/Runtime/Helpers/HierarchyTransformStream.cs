using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

using Unity.ClusterDisplay.RPC;

namespace Unity.ClusterDisplay
{
    [RequireComponent(typeof(Transform))]
    public class HierarchyTransformStream : MonoBehaviour
    {
        [HideInInspector][SerializeField] private Transform rootTransform;
        public Transform RootTransform
        {
            get
            {
                if (rootTransform == null)
                {
                    rootTransform = GetComponent<Transform>();
                    cachedChildTransforms = GetComponentsInChildren<Transform>();
                }

                return rootTransform;
            }
        }

        private Transform[] cachedChildTransforms;

        public int TransformCount => cachedChildTransforms == null ? 1 : cachedChildTransforms.Length + 1;
        private NativeArray<Data> cachedTransformData;

        public struct Data
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private void CacheHierarchyTransformations ()
        {
            if (cachedTransformData == null || cachedTransformData.Length != cachedChildTransforms.Length)
                cachedTransformData = new NativeArray<Data>(cachedChildTransforms.Length, Allocator.Persistent);

            for (int i = 0; i < cachedChildTransforms.Length; i++)
                cachedTransformData[i] = new Data
                {
                    localPosition = cachedChildTransforms[i].localPosition,
                    localRotation = cachedChildTransforms[i].localRotation,
                    localScale = cachedChildTransforms[i].localScale
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

            for (int i = 0; i < cachedChildTransforms.Length; i++)
            {
                cachedChildTransforms[i].localPosition = transformations[i].localPosition;
                cachedChildTransforms[i].localRotation = transformations[i].localRotation;
                cachedChildTransforms[i].localScale = transformations[i].localScale;
            }
        }
    }
}
