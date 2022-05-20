using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Message containing transform data (plus unique identifier)
    /// </summary>
    public readonly struct TransformMessage
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public TransformMessage(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public TransformMessage(Transform t)
            : this(t.localPosition, t.localRotation, t.localScale) { }
    }

    class TransformReplicator : ReplicatorBase<TransformMessage>
    {
        Transform m_Target;

        public TransformReplicator(Guid guid, Transform target)
            : base(guid)
        {
            ClusterDebug.Log("TransformReplicator created!");
            m_Target = target;
        }

        protected override TransformMessage GetCurrentState() => new(m_Target);

        protected override void ApplyMessage(in TransformMessage message)
        {
#if UNITY_EDITOR
            return;
#endif
            Debug.Log("Transform Replicator ApplyMessage");
            m_Target.localPosition = message.Position;
            m_Target.localRotation = message.Rotation;
            m_Target.localScale = message.Scale;
        }
    }
}
