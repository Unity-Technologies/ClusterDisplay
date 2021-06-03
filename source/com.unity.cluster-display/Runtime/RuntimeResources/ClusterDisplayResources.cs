using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This scriptable object can be thought of as an (X)RP RenderPipelineAsset for configuration
/// but for cluster display. There is a default instance of this scriptable object inside
/// the cluster display package that automatically gets loaded when the ClusterRenderer
/// class is initialized.
/// </summary>
[CreateAssetMenu(fileName = "Data", menuName = "Cluster Display/ClusterDisplayResources", order = 1)]
public class ClusterDisplayResources : ScriptableObject
{
    [SerializeField] private uint m_MaxFrameNetworkByteBufferSize = 32 * 1024;
    [SerializeField] private uint m_MaxRpcByteBufferSize = 16 * 1024;

    public uint MaxFrameNetworkByteBufferSize => m_MaxFrameNetworkByteBufferSize;
    public uint MaxRpcByteBufferSize => m_MaxRpcByteBufferSize;

    private void OnValidate()
    {
        if (m_MaxRpcByteBufferSize > m_MaxFrameNetworkByteBufferSize)
        {
            Debug.LogError($"RPC byte buffer cannot be larger then the frame network byte buffer size.");
            m_MaxRpcByteBufferSize = m_MaxFrameNetworkByteBufferSize;
        }
    }
}
