using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    /// <summary>
    /// Message containing transform data (plus unique identifier)
    /// </summary>
    public readonly struct TransformMessage : IEquatable<TransformMessage>
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

        public override string ToString()
        {
            return $"[{Position}, {Rotation}, {Scale}]";
        }

        public bool Equals(TransformMessage other)
        {
            return Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
        }

        public override bool Equals(object obj)
        {
            return obj is TransformMessage other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, Rotation, Scale);
        }

        public static bool operator ==(TransformMessage left, TransformMessage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TransformMessage left, TransformMessage right)
        {
            return !left.Equals(right);
        }
    }

    [SpecializedReplicator(typeof(Transform))]
    class TransformReplicator : ReplicatorBase<TransformMessage>
    {
        Transform m_Target;

        public TransformReplicator(Guid guid, Transform target)
            : base(guid)
        {
            ClusterDebug.Log("TransformReplicator created");
            m_Target = target;
        }

        public override bool IsValid => m_Target != null;

        /// <inheritdoc />
        protected override TransformMessage GetCurrentState() => new(m_Target);

        /// <inheritdoc />
        protected override void ApplyMessage(in TransformMessage message)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            m_Target.localPosition = message.Position;
            m_Target.localRotation = message.Rotation;
            m_Target.localScale = message.Scale;
        }
    }
}
