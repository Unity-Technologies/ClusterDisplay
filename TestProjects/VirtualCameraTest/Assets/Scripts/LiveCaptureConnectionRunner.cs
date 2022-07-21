using System;
using System.Reflection;
using Unity.ClusterDisplay;
using Unity.LiveCapture;
using UnityEngine;

/// <summary>
/// Script to enable a Companion App connection in a standalone player.
/// </summary>
/// <remarks>
/// When running as part of a cluster, this script will only execute on the emitter.
/// </remarks>
public class LiveCaptureConnectionRunner : MonoBehaviour
{
    const string k_ServerTypeName = "Unity.LiveCapture.CompanionApp.CompanionAppServer";
    const string k_CompanionAppAssembly = "Unity.LiveCapture.CompanionApp";

    ConnectionManager m_ConnectionManager;
    MethodInfo m_UpdateMethod;
    MethodInfo m_StartMethod;
    MethodInfo m_StopMethod;
    Connection m_Server;

    bool m_ShouldRun;

    void Awake()
    {
        m_ConnectionManager = ConnectionManager.Instance;
        m_UpdateMethod = typeof(ConnectionManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        var serverType = Type.GetType($"{k_ServerTypeName}, {k_CompanionAppAssembly}");
        if (serverType != null)
        {
            m_Server = m_ConnectionManager.CreateConnection(serverType);
            m_StartMethod = serverType.GetMethod("StartServer");
            m_StopMethod = serverType.GetMethod("StopServer");
        }

        if (m_UpdateMethod == null || m_Server == null || m_StartMethod == null || m_StopMethod == null)
        {
            Debug.LogError($"Unable to set up a Companion App connection. Did you import the correct LiveCapture package?");
        }
    }

    void OnEnable()
    {
        m_ShouldRun = ClusterDisplayState.GetNodeRole() is not NodeRole.Repeater && m_Server != null;

        // In the editor, StartServer() is automatically called when entering play mode.
#if !UNITY_EDITOR
        if (m_ShouldRun)
        {
            m_StartMethod?.Invoke(m_Server, null);
        }
#endif
    }

    void OnDisable()
    {
        if (m_Server != null && m_ShouldRun)
        {
            m_StopMethod?.Invoke(m_Server, null);
        }
    }

    // Update is called once per frame
    void Update()
    {
        m_UpdateMethod?.Invoke(m_ConnectionManager, null);
    }
}
