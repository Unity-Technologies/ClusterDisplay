using Unity.Collections;
using UnityEngine;

using Unity.ClusterDisplay.RPC;

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

        [SerializeField][HideInInspector] private Transform[] cachedTransforms = null;

        private NativeArray<Data> cachedData;

        protected abstract void CacheTransforms();
        protected void ApplyCachedTransforms (Transform[] cachedTransforms) => this.cachedTransforms = cachedTransforms;

        public virtual void ApplyTransformData(NativeSlice<Data> data)
        {
            if (ClusterDisplayState.IsMaster)
                return;

            if (cachedTransforms == null)
            {
                CacheTransforms();
                if (cachedTransforms == null)
                    return;
            }

            int di = 0;
            for (int ti = 0; ti < cachedTransforms.Length; ti++)
            {
                if (cachedTransforms[ti] == null)
                    continue;

                cachedTransforms[ti].localPosition  = data[di].localPosition;
                cachedTransforms[ti].localRotation  = data[di].localRotation;
                cachedTransforms[ti].localScale     = data[di++].localScale;
            }
        }

        public bool TryGetData (out NativeSlice<Data> outCachedData)
        {
            if (cachedTransforms == null)
            {
                outCachedData = default(NativeArray<Data>);
                return true;
            }

            if (!cachedData.IsCreated || cachedData.Length != cachedTransforms.Length)
            {
                cachedData = new NativeArray<Data>(cachedTransforms.Length, Allocator.Persistent);
                cachedTransforms = new Transform[cachedTransforms.Length];
            }

            int di = 0;
            for (int ti = 0; ti < cachedTransforms.Length; ti++)
            {
                if (cachedTransforms[ti] == null)
                    continue;

                cachedData[di++] = new Data
                {
                    localPosition   = cachedTransforms[ti].localPosition,
                    localRotation   = cachedTransforms[ti].localRotation,
                    localScale      = cachedTransforms[ti].localScale
                };
            }

            outCachedData = cachedData.Slice(0, di);
            return true;
        }
    }
}
