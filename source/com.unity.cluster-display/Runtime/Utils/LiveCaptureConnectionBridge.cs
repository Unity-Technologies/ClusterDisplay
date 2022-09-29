#if LIVE_CAPTURE_2_0_OR_NEWER
using System;
using System.Reflection;
using Unity.LiveCapture;
using UnityEngine;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// Script to enable a Live Capture connection in a standalone player.
    /// </summary>
    /// <remarks>
    /// When running as part of a cluster, this script will only execute on the emitter.
    /// </remarks>
    public class LiveCaptureConnectionBridge : MonoBehaviour, ISerializationCallbackReceiver
    {
        static readonly MethodInfo k_UpdateMethod = typeof(ConnectionManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo m_StartMethod;
        MethodInfo m_StopMethod;

        [SerializeField]
        string m_ConnectionTypeName;

        internal Type m_ConnectionType;

        ConnectionManager m_ConnectionManager;
        Connection m_Connection;

        bool m_ShouldRun;

        void Awake()
        {
            m_ConnectionManager = ConnectionManager.Instance;

            if (k_UpdateMethod == null)
            {
                Debug.LogError("Unable to set up a Companion App connection. Did you import the correct LiveCapture package?");
            }
        }

        void OnEnable()
        {
            m_ShouldRun = ClusterDisplayState.GetNodeRole() is not NodeRole.Repeater;

            if (!m_ShouldRun) return;

            if (m_Connection == null)
            {
                m_Connection = m_ConnectionManager.CreateConnection(m_ConnectionType);
            }

            // In the editor, StartServer() is automatically called when entering play mode.
#if !UNITY_EDITOR
            m_StartMethod?.Invoke(m_Connection, null);
#endif
        }

        void OnDisable()
        {
            if (m_Connection != null && m_ShouldRun)
            {
                m_StopMethod?.Invoke(m_Connection, null);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (!m_ShouldRun) return;
            k_UpdateMethod?.Invoke(m_ConnectionManager, null);
            // Debug.Log($"Connection update {k_UpdateMethod?.Name}");
        }

        public void OnBeforeSerialize()
        {
            m_ConnectionTypeName = m_ConnectionType?.AssemblyQualifiedName;
        }

        public void OnAfterDeserialize()
        {
            OnDisable();
            m_ConnectionType = Type.GetType(m_ConnectionTypeName);
            m_StartMethod = m_ConnectionType?.GetMethod("StartServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m_StopMethod = m_ConnectionType?.GetMethod("StopServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Debug.Log($"Connection type: {m_ConnectionType?.Name}, {m_StartMethod?.Name}, {m_StopMethod?.Name}");
        }
    }
}

#endif
