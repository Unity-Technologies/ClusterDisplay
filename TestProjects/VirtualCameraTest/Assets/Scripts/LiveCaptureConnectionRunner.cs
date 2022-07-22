using System;
using System.Reflection;
using Unity.ClusterDisplay;
using Unity.LiveCapture;
using Unity.LiveCapture.VirtualCamera;
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

    static readonly Type k_ServerType = Type.GetType($"{k_ServerTypeName}, {k_CompanionAppAssembly}");
    static readonly MethodInfo k_UpdateMethod = typeof(ConnectionManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo k_StartMethod= k_ServerType?.GetMethod("StartServer");
    static readonly MethodInfo k_StopMethod = k_ServerType?.GetMethod("StopServer");

    ConnectionManager m_ConnectionManager;
    Connection m_Server;

    bool m_ShouldRun;

    void Awake()
    {
        m_ConnectionManager = ConnectionManager.Instance;
        m_Server = m_ConnectionManager.CreateConnection(k_ServerType);

        if (k_UpdateMethod == null || m_Server == null || k_StartMethod == null || k_StopMethod == null)
        {
            Debug.LogError("Unable to set up a Companion App connection. Did you import the correct LiveCapture package?");
        }
    }

    void OnEnable()
    {
        m_ShouldRun = ClusterDisplayState.GetNodeRole() is not NodeRole.Repeater && m_Server != null;

        if (m_ShouldRun)
        {
            // In the editor, StartServer() is automatically called when entering play mode.
#if !UNITY_EDITOR
            m_StartMethod?.Invoke(m_Server, null);
#endif
        }
    }

    void OnDisable()
    {
        if (m_Server != null && m_ShouldRun)
        {
            k_StopMethod?.Invoke(m_Server, null);
        }
    }

    // Update is called once per frame
    void Update()
    {
        k_UpdateMethod?.Invoke(m_ConnectionManager, null);
    }
}
