using System;
using System.Net;
using UnityEngine;

namespace Unity.ClusterDisplay.Scripting
{
    [CreateAssetMenu(fileName = "EditorLink", menuName = "Cluster Display/Editor Link")]
    public class EditorLinkConfig : ScriptableObject, ISerializationCallbackReceiver, IEquatable<EditorLinkConfig>
    {
        [SerializeField]
        string m_Address = "127.0.0.1";

        [SerializeField]
        int m_Port = 40000;

        public IPEndPoint EndPoint { get; private set; }

        public IPAddress Address { get; private set; }

        public int Port => m_Port;

        public override string ToString() => EndPoint.ToString();

        public void Parse(string s)
        {
            var parts = s.Split(':');
            if (parts.Length != 2)
            {
                throw new FormatException();
            }

            m_Address = parts[0];
            m_Port = int.Parse(parts[1]);

            OnAfterDeserialize();
        }

        public bool Equals(EditorLinkConfig other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && m_Address == other.m_Address && m_Port == other.m_Port;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EditorLinkConfig)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), m_Address, m_Port);
        }

        public static bool operator ==(EditorLinkConfig left, EditorLinkConfig right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EditorLinkConfig left, EditorLinkConfig right)
        {
            return !Equals(left, right);
        }

        public void OnBeforeSerialize()
        {
            // Nothing to do.
        }

        public void OnAfterDeserialize()
        {
            Address = IPAddress.Parse(m_Address);
            EndPoint = new IPEndPoint(Address, m_Port);
        }
    }
}
