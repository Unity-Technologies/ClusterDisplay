using Unity.Collections;
using System.Linq;
using UnityEngine;
using Unity.Jobs;

using Unity.ClusterDisplay.RPC;
using UnityEngine.Jobs;
using Unity.Burst;

namespace Unity.ClusterDisplay
{
    public abstract class TransformHierarchyStreamBase : MonoBehaviour
    {
        public struct Data
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private Transform[] cachedTransforms = null;
        protected abstract void CacheTransforms();
        protected void ApplyCachedTransforms (Transform[] cachedTransforms) => this.cachedTransforms = cachedTransforms;

        private CacheDataJob cacheDataJob = new CacheDataJob();
        private ApplyDataJob applyDataJob = new ApplyDataJob();

        // [BurstCompile]
        public struct CacheDataJob : IJobParallelForTransform
        {
            public NativeArray<Data> cachedData;
            public void Execute(int index, TransformAccess transform)
            {
                cachedData[index] = new Data
                {
                    localPosition = transform.localPosition,
                    localRotation = transform.localRotation,
                    localScale = transform.localScale
                };
            }
        }

        // [BurstCompile]
        public struct ApplyDataJob : IJobParallelForTransform
        {
            public NativeSlice<Data> cachedData;
            public void Execute(int index, TransformAccess transform)
            {
                transform.localPosition = cachedData[index].localPosition;
                transform.localRotation = cachedData[index].localRotation;
                transform.localScale = cachedData[index].localScale;
            }
        }

        private void OnDestroy()
        {
            if (cacheDataJob.cachedData.IsCreated)
                cacheDataJob.cachedData.Dispose();
        }

        private void ValidateCachedData ()
        {
            if (cachedTransforms == null || cachedTransforms.Length == 0)
                CacheTransforms();

            if (!cacheDataJob.cachedData.IsCreated || cachedTransforms.Length != cacheDataJob.cachedData.Length)
                cacheDataJob.cachedData = new NativeArray<Data>(cachedTransforms.Length, Allocator.Persistent);
        }

        private void LateUpdate ()
        {
            if (!ClusterDisplayState.IsMaster)
                return;

            if (!TryGetData(out var data))
                return;

            this.ApplyTransformData(data);
        }

        public virtual void ApplyTransformData(NativeArray<Data> data)
        {
            if (ClusterDisplayState.IsMaster)
                return;

            ValidateCachedData();
            if (cachedTransforms == null)
                return;

            var transformAccessArray = new TransformAccessArray(cachedTransforms);
            applyDataJob.cachedData = data;

            var jobHandle = applyDataJob.Schedule(transformAccessArray);
            jobHandle.Complete();
        }

        public bool TryGetData (out NativeArray<Data> outCachedData)
        {
            ValidateCachedData();
            if (cachedTransforms == null)
            {
                outCachedData = default(NativeArray<Data>);
                return true;
            }

            var transformAccessArray = new TransformAccessArray(cachedTransforms);
            var jobHandle = cacheDataJob.Schedule(transformAccessArray);
            jobHandle.Complete();

            outCachedData = cacheDataJob.cachedData;
            return true;
        }
    }
}
