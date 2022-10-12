#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Collections;
using UnityEngine.InputSystem.LowLevel;
using Unity.ClusterDisplay;

public class InputSystemReplicator : MonoBehaviour
{
    const int k_CaptureMemoryDefaultSize = 512;
    const int k_CaptureMemoryMaxSize = 1024;

    readonly InputEventTrace m_EventTrace = new(k_CaptureMemoryDefaultSize, growBuffer: true, k_CaptureMemoryMaxSize);
    InputEventTrace.ReplayController m_ReplayController;
    readonly MemoryStream m_MemoryStream = new(k_CaptureMemoryMaxSize);

    void OnEnable()
    {
        switch (ClusterDisplayState.GetNodeRole())
        {
            case NodeRole.Emitter:
                m_EventTrace.Enable();
                EmitterStateWriter.RegisterOnStoreCustomDataDelegate((int)StateID.InputSystem, OnStoreInputData);
                break;
            case NodeRole.Repeater:
                RepeaterStateReader.RegisterOnLoadDataDelegate((int)StateID.InputSystem, OnLoadInputData);
                break;
            case NodeRole.Unassigned:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    bool OnLoadInputData(NativeArray<byte> stateData)
    {
        if (stateData.Length == 0) return true;

        m_MemoryStream.Write(stateData.AsReadOnlySpan());
        m_MemoryStream.Flush();
        m_MemoryStream.Position = 0;
        m_EventTrace.ReadFrom(m_MemoryStream);

        m_ReplayController?.Dispose();
        m_ReplayController = m_EventTrace.Replay().PlayAllEvents();

        m_MemoryStream.SetLength(0);
        return true;
    }

    int OnStoreInputData(NativeArray<byte> writeableBuffer)
    {
        if (writeableBuffer.Length < m_MemoryStream.Length)
        {
            ClusterDebug.LogWarning($"memory stream {m_MemoryStream.Length} to large for buffer {writeableBuffer.Length}. Retrying...");
            return -1;
        }

        int bytesRead = m_MemoryStream.Read(writeableBuffer.AsSpan());
        Debug.Log($"sending input data {bytesRead}");
        return bytesRead;
    }

    void OnDisable()
    {
        m_EventTrace.Disable();
        m_ReplayController?.Dispose();
    }

    void OnDestroy()
    {
        m_EventTrace.Dispose();
    }

    void Update()
    {
        if (ClusterDisplayState.GetNodeRole() is NodeRole.Emitter)
        {
            m_MemoryStream.SetLength(0);
            m_EventTrace.WriteTo(m_MemoryStream);
            Debug.Log($"captured: {m_EventTrace.totalEventSizeInBytes}, in-stream {m_MemoryStream.Length}");
            m_MemoryStream.Flush();
            m_MemoryStream.Position = 0;
        }

        m_EventTrace.Clear();
    }
}
#endif
