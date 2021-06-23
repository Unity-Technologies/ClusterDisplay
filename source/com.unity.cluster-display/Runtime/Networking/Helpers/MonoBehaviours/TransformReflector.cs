using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.Networking
{
    [RequireComponent(typeof(Transform))]
    public partial class TransformReflector : ComponentReflector<Transform>
    {
        [System.Flags]
        public enum TransformReflectorMode
        {
            None = 0,
            Position = 1,
            Rotation = 2,
            Scale = 4
        }

        public TransformReflectorMode mode;

        protected override void OnCache()
        {
        }

        public Vector3 position
        {
            get => transform.position;
            [ClusterRPC] set => transform.position = value;
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            [ClusterRPC] set => transform.rotation = value;
        }

        public Vector3 scale
        {
            get => transform.localScale;
            [ClusterRPC] set => transform.localScale = value;
        }
    }
}
