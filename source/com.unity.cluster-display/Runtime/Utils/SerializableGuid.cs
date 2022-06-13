using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// A struct containing a 128-bit GUID that is compatible with Unity serialization.
    /// </summary>
    [Serializable]
    public class SerializableGuid : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        bool m_Serialized;

        [SerializeField]
        long m_Low;

        [SerializeField]
        long m_High;

        public SerializableGuid()
        {
            FromGuid(Guid.NewGuid());
        }

        public static implicit operator Guid(SerializableGuid guid)
        {
            Span<byte> bytes = stackalloc byte[16];
            BitConverter.TryWriteBytes(bytes[..8], guid.m_Low);
            BitConverter.TryWriteBytes(bytes[8..], guid.m_High);
            return new Guid(bytes);
        }

        public override string ToString() => ((Guid)this).ToString();

        public void FromGuid(Guid guid)
        {
            ReadOnlySpan<byte> bytes = guid.ToByteArray();
            m_Low = BitConverter.ToInt64(bytes[..8]);
            m_High = BitConverter.ToInt64(bytes[8..]);
        }

        public void OnBeforeSerialize()
        {
            // Hack: Generate a new GUID (instead of setting members to 0)
            // when a new instance is created in the Editor.

            if (m_Serialized) return;

            FromGuid(Guid.NewGuid());
            m_Serialized = true;
        }

        public void OnAfterDeserialize()
        {
        }
    }
}
