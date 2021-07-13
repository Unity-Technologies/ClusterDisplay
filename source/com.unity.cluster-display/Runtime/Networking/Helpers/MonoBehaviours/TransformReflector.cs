using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.ClusterDisplay.RPC
{
    class RPCFlagEnumState : System.Attribute {}

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

        public TransformReflectorMode m_Mode;

        public Vector3 position
        {
            get => m_TargetInstance.position;
            [ClusterRPC] set => m_TargetInstance.position = value;
        }

        public Quaternion rotation
        {
            get => m_TargetInstance.rotation;
            [ClusterRPC] set => m_TargetInstance.rotation = value;
        }

        public Vector3 scale
        {
            get => m_TargetInstance.localScale;
            [ClusterRPC] set => m_TargetInstance.localScale = value;
        }
    }
}
