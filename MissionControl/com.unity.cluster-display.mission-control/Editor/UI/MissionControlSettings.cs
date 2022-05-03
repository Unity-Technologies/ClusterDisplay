using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.ClusterDisplay.MissionControl.Editor
{
    [Serializable]
    struct MutableNodeSettings : IEquatable<MutableNodeSettings>
    {
        public int Id;
        public bool IsActive;
        public int ClusterId;

        public MutableNodeSettings(int id)
        {
            Id = id;
            IsActive = true;
            ClusterId = 0;
        }

        public bool Equals(MutableNodeSettings other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is MutableNodeSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(MutableNodeSettings left, MutableNodeSettings right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MutableNodeSettings left, MutableNodeSettings right)
        {
            return !left.Equals(right);
        }
    }

    [FilePath("ClusterDisplay/MissionControl.asset", FilePathAttribute.Location.PreferencesFolder)]
    class MissionControlSettings : ScriptableSingleton<MissionControlSettings>
    {
        [SerializeField]
        string m_RootPath;

        [SerializeField]
        int m_HandshakeTimeout = 10000;

        [SerializeField]
        int m_CommTimeout = 5000;

        [SerializeField]
        bool m_DeleteRegistryKey;

        [SerializeField]
        private string m_NetworkAdapterName;

        [SerializeField]
        string m_BroadcastProxyAddress;

        [SerializeField]
        List<MutableNodeSettings> m_NodeSettings = new();

        [SerializeField]
        bool m_UseDeprecatedArgs = false;

        [SerializeField]
        string m_ExtraArgs;

        public string RootPath
        {
            get => m_RootPath;
            set => m_RootPath = value;
        }

        public int HandshakeTimeout
        {
            get => m_HandshakeTimeout;
            set => m_HandshakeTimeout = value;
        }

        public int Timeout
        {
            get => m_CommTimeout;
            set => m_CommTimeout = value;
        }

        public bool DeleteRegistryKey
        {
            get => m_DeleteRegistryKey;
            set => m_DeleteRegistryKey = value;
        }

        public string NetworkAdapterName
        {
            get => m_NetworkAdapterName;
            set => m_NetworkAdapterName = value;
        }

        public string BroadcastProxyAddress
        {
            get => m_BroadcastProxyAddress;
            set => m_BroadcastProxyAddress = value;
        }

        public List<MutableNodeSettings> NodeSettings
        {
            get => m_NodeSettings;
            set => m_NodeSettings = value;
        }

        public bool UseDeprecatedArgs
        {
            get => m_UseDeprecatedArgs;
            set => m_UseDeprecatedArgs = value;
        }

        public string ExtraArgs
        {
            get => m_ExtraArgs;
            set => m_ExtraArgs = value;
        }

        public void Save()
        {
            Save(true);
        }
    }
}
